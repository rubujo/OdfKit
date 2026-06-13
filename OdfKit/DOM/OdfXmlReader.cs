using System.Security;
using System.Xml;
using OdfKit.Core;
using CommunityToolkit.HighPerformance.Buffers;

namespace OdfKit.DOM;

/// <summary>
/// 用於將 XML 串流解析為 ODF DOM 節點樹的讀取器類別。
/// </summary>
public static class OdfXmlReader
{
    /// <summary>
    /// 元素巢狀深度的最大限制，用於防禦阻斷服務 (DoS) 攻擊。
    /// </summary>
    public const int MaxElementDepth = 256;

    /// <summary>
    /// 將 ODF XML 檔案串流解析為記憶體 DOM 節點樹。
    /// </summary>
    /// <param name="stream">要解析的 XML 輸入串流</param>
    /// <param name="options">載入選項；如果為 <see langword="null"/>，則使用預設選項</param>
    /// <returns>解析完成的根元素節點</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="stream"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="SecurityException">當 XML 巢狀深度超過限制時擲出</exception>
    /// <exception cref="InvalidDataException">當 XML 結構無效 (例如找不到根元素) 時擲出</exception>
    public static OdfNode Parse(Stream stream, OdfLoadOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        options ??= OdfLoadOptions.Default;

        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit, // XXE 防禦
            XmlResolver = null,                     // XXE 防禦
            IgnoreWhitespace = false,               // 保留有意義的空白
            IgnoreComments = false,
            IgnoreProcessingInstructions = false
        };

        // StringPool 用於重複使用常見的字串執行個體 (例如元素與屬性名稱、命名空間)，
        // 以在解析大型 XML 檔案時大幅減少 GC 記憶體配置。
        StringPool pool = new();

        OdfNode? rootNode = null;
        Stack<OdfNode> stack = new();
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
                            // 堆疊溢位 DoS 防禦保護
                            if (currentDepth > MaxElementDepth)
                            {
                                throw new SecurityException($"XML element nesting depth limit exceeded ({currentDepth} > {MaxElementDepth}). Potential StackOverflow attack.");
                            }

                            string localName = pool.GetOrAdd(reader.LocalName);
                            string nsUri = pool.GetOrAdd(reader.NamespaceURI);
                            string prefix = pool.GetOrAdd(reader.Prefix);

                            var node = OdfNodeFactory.CreateElement(localName, nsUri, prefix);

                            // 解析屬性
                            if (reader.HasAttributes)
                            {
                                while (reader.MoveToNextAttribute())
                                {
                                    string attrLocalName = pool.GetOrAdd(reader.LocalName);
                                    string attrNsUri = pool.GetOrAdd(reader.NamespaceURI);
                                    string attrPrefix = pool.GetOrAdd(reader.Prefix);
                                    string attrValue = reader.Value; // 一般不在 StringPool 中快取屬性值，因為其值變化較大，除非是空值或布林值

                                    // 跳過命名空間宣告 (xmlns 或 xmlns:prefix)
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
                            else if (rootNode is null)
                            {
                                rootNode = node;
                            }

                            if (!reader.IsEmptyElement)
                            {
                                stack.Push(node);
                            }
                            else
                            {
                                // 空元素 (<element />) 不會推入堆疊，因此立即遞減深度
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

                        case XmlNodeType.Comment:
                            if (stack.Count > 0)
                            {
                                string commentVal = reader.Value;
                                OdfNode commentNode = new(OdfNodeType.Comment, string.Empty, string.Empty)
                                {
                                    TextContent = commentVal
                                };
                                stack.Peek().AppendChild(commentNode);
                            }
                            break;

                        case XmlNodeType.ProcessingInstruction:
                            if (stack.Count > 0)
                            {
                                OdfNode instructionNode = new(OdfNodeType.ProcessingInstruction, reader.Name, string.Empty)
                                {
                                    TextContent = reader.Value
                                };
                                stack.Peek().AppendChild(instructionNode);
                            }
                            break;

                        case XmlNodeType.Text:
                        case XmlNodeType.SignificantWhitespace:
                        case XmlNodeType.Whitespace:
                            if (stack.Count > 0)
                            {
                                string textVal = reader.Value;
                                OdfNode textNode = new(OdfNodeType.Text, string.Empty, string.Empty)
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

        if (rootNode is null)
        {
            throw new InvalidDataException("Invalid XML structure: Root element not found.");
        }

        return rootNode;
    }
}
