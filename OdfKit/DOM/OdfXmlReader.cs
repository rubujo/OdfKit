using System.Security;
using System.Xml;
using OdfKit.Core;
using CommunityToolkit.HighPerformance.Buffers;

namespace OdfKit.DOM
{
    public static class OdfXmlReader
    {
        public const int MaxElementDepth = 256;

        /// <summary>
        /// 將 ODF XML 檔案串流解析為記憶體 DOM 節點樹。
        /// </summary>
        public static OdfNode Parse(Stream stream, OdfLoadOptions? options = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            options ??= OdfLoadOptions.Default;

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, // XXE 防禦
                XmlResolver = null,                     // XXE 防禦
                IgnoreWhitespace = false,               // 保留有意義的空白
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            };

            // StringPool is used to reuse common string instances (like element/attribute names, namespaces)
            // which dramatically reduces GC allocations when parsing huge XML files.
            var pool = new StringPool();

            OdfNode? rootNode = null;
            var stack = new Stack<OdfNode>();
            int currentDepth = 0;

            using (var reader = XmlReader.Create(stream, settings))
            {
                try
                {
                    while (reader.Read())
                    {
                        switch (reader.NodeType)
                        {
                            case XmlNodeType.Element:
                                currentDepth++;
                                // StackOverflow DoS protection
                                if (currentDepth > MaxElementDepth)
                                {
                                    throw new SecurityException($"XML element nesting depth limit exceeded ({currentDepth} > {MaxElementDepth}). Potential StackOverflow attack.");
                                }

                                string localName = pool.GetOrAdd(reader.LocalName);
                                string nsUri = pool.GetOrAdd(reader.NamespaceURI);
                                string prefix = pool.GetOrAdd(reader.Prefix);

                                var node = new OdfNode(OdfNodeType.Element, localName, nsUri, prefix);

                                // Parse attributes
                                if (reader.HasAttributes)
                                {
                                    while (reader.MoveToNextAttribute())
                                    {
                                        string attrLocalName = pool.GetOrAdd(reader.LocalName);
                                        string attrNsUri = pool.GetOrAdd(reader.NamespaceURI);
                                        string attrPrefix = pool.GetOrAdd(reader.Prefix);
                                        string attrValue = reader.Value; // We don't cache attribute values in StringPool generally as they vary widely, except empty or boolean values

                                        // Skip namespace declarations (xmlns or xmlns:prefix)
                                        if (attrNsUri == "http://www.w3.org/2000/xmlns/" || attrLocalName == "xmlns" || attrPrefix == "xmlns")
                                        {
                                            continue;
                                        }

                                        node.SetAttribute(attrLocalName, attrNsUri, attrValue, attrPrefix);
                                    }
                                    reader.MoveToElement();
                                }

                                if (stack.Count > 0)
                                {
                                    stack.Peek().AppendChild(node);
                                }
                                else if (rootNode == null)
                                {
                                    rootNode = node;
                                }

                                if (!reader.IsEmptyElement)
                                {
                                    stack.Push(node);
                                }
                                else
                                {
                                    // Empty elements (<element />) don't push to stack, so decrement depth immediately
                                    currentDepth--;
                                }
                                break;

                            case XmlNodeType.EndElement:
                                if (stack.Count > 0)
                                {
                                    stack.Pop();
                                }
                                currentDepth--;
                                break;

                            case XmlNodeType.Text:
                            case XmlNodeType.SignificantWhitespace:
                            case XmlNodeType.Whitespace:
                                if (stack.Count > 0)
                                {
                                    string textVal = reader.Value;
                                    var textNode = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty)
                                    {
                                        TextContent = textVal
                                    };
                                    stack.Peek().AppendChild(textNode);
                                }
                                break;
                        }
                    }
                }
                catch (XmlException ex)
                {
                    if (!options.StrictXmlParsing)
                    {
                        OdfKitDiagnostics.Warn($"Lax parsing XML exception: {ex.Message}. Attempting to salvage partial DOM tree.", ex);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            if (rootNode == null)
            {
                throw new InvalidDataException("Invalid XML structure: Root element not found.");
            }

            return rootNode;
        }
    }
}
