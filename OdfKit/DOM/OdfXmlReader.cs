using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;
using CommunityToolkit.HighPerformance.Buffers;
using CommunityToolkit.HighPerformance;
using OdfKit.Core;

using OdfKit.Compliance;
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
    /// 將 ODF XML 唯讀位元組區段解析為記憶體 DOM 節點樹。
    /// </summary>
    /// <param name="xmlData">包含 XML 資料的唯讀位元組區段</param>
    /// <param name="options">載入選項；如果為 <see langword="null"/>，則使用預設選項</param>
    /// <returns>解析完成的根元素節點</returns>
    public static OdfNode Parse(ReadOnlyMemory<byte> xmlData, OdfLoadOptions? options = null)
    {
        return Parse(xmlData, IntPtr.Zero, options);
    }

    /// <summary>
    /// 將 ODF XML 唯讀位元組區段與基底指標解析為記憶體 DOM 節點樹。
    /// </summary>
    public static OdfNode Parse(ReadOnlyMemory<byte> xmlData, IntPtr basePtr, OdfLoadOptions? options = null)
    {
        options ??= OdfLoadOptions.Default;
        if (!options.AllowLazyLoading)
        {
            using var stream = xmlData.AsStream();
            return Parse(stream, options);
        }

        var stopwatch = Stopwatch.StartNew();
        OdfNode? rootNode = null;
        Stack<OdfNode> stack = new();
        Stack<Dictionary<string, string>> namespaceScopes = new();
        int currentDepth = 0;
        long xmlCharacterCount = 0;

        var reader = new OdfUtf8XmlReader(xmlData.Span);
        OdfUtf8XmlToken token;
        while (reader.Read(out token))
        {
            switch (token.Kind)
            {
                case OdfUtf8XmlTokenKind.StartElement:
                case OdfUtf8XmlTokenKind.SelfClosingElement:
                    currentDepth++;
                    if (currentDepth > MaxElementDepth)
                    {
                        throw new SecurityException(OdfLocalizer.GetMessage("Err_OdfXmlReader_XmlElementNestingDepth", CultureInfo.InvariantCulture, currentDepth, MaxElementDepth));
                    }

                    Dictionary<string, string> elementNamespaces = namespaceScopes.Count > 0
                        ? new Dictionary<string, string>(namespaceScopes.Peek(), StringComparer.Ordinal)
                        : new Dictionary<string, string>(StringComparer.Ordinal);
                    AddNamespaceDeclarations(token.Attributes, elementNamespaces);
                    ResolveQualifiedName(token.Name, elementNamespaces, out string prefix, out string localName, out string nsUri);

                    AddXmlCharacters(options, ref xmlCharacterCount, localName.Length + nsUri.Length + prefix.Length);

                    var node = OdfNodeFactory.CreateElement(localName, nsUri, prefix);

                    // 解析屬性
                    if (!token.Attributes.IsEmpty)
                    {
                        ParseAttributes(token.Attributes, node, elementNamespaces, ref xmlCharacterCount, options);
                    }

                    if (stack.Count > 0)
                    {
                        stack.Peek().AppendChild(node);
                    }
                    else if (rootNode is null)
                    {
                        rootNode = node;
                    }

                    bool shouldLazy = false;
                    if (token.Kind == OdfUtf8XmlTokenKind.StartElement)
                    {
                        if (localName == "table" && nsUri == "urn:oasis:names:tc:opendocument:xmlns:table:1.0")
                        {
                            shouldLazy = true;
                        }
                        else if (localName == "meta" || localName == "settings" || localName == "styles" || localName == "p" || localName == "list")
                        {
                            shouldLazy = true;
                        }
                    }

                    if (shouldLazy)
                    {
                        int innerStart = token.Offset + token.Length;
                        string fullElementName = prefix.Length == 0 ? localName : string.Concat(prefix, ":", localName);
                        if (TryFindEndElementOffset(reader, fullElementName, out int innerEnd, out OdfUtf8XmlReader lazyReader))
                        {
                            int innerLen = innerEnd - innerStart;
                            if (innerLen >= 8192)
                            {
                                node._xmlByteRange = new OdfXmlByteRange(
                                    token.Offset,
                                    lazyReader.Position - token.Offset,
                                    innerStart,
                                    innerLen);
                                if (basePtr != IntPtr.Zero)
                                {
                                    node._lazyXmlPtr = basePtr + innerStart;
                                    node._lazyXmlLen = innerLen;
                                }
                                else
                                {
                                    node._lazyXmlMemory = xmlData.Slice(innerStart, innerLen);
                                }
                                node._isLazy = true;
                                reader = lazyReader;
                            }
                            else
                            {
                                shouldLazy = false;
                            }
                        }
                        else
                        {
                            shouldLazy = false;
                        }
                    }

                    if (!shouldLazy && token.Kind == OdfUtf8XmlTokenKind.StartElement)
                    {
                        stack.Push(node);
                        namespaceScopes.Push(elementNamespaces);
                    }
                    else if (token.Kind == OdfUtf8XmlTokenKind.SelfClosingElement || shouldLazy)
                    {
                        currentDepth--;
                    }
                    break;

                case OdfUtf8XmlTokenKind.EndElement:
                    if (stack.Count > 0)
                    {
                        stack.Pop();
                    }
                    if (namespaceScopes.Count > 0)
                    {
                        namespaceScopes.Pop();
                    }
                    currentDepth--;
                    break;

                case OdfUtf8XmlTokenKind.Comment:
                    if (stack.Count > 0)
                    {
                        string commentVal = token.GetValueString();
                        AddXmlCharacters(options, ref xmlCharacterCount, commentVal.Length);
                        OdfNode commentNode = new(OdfNodeType.Comment, string.Empty, string.Empty)
                        {
                            TextContent = commentVal
                        };
                        stack.Peek().AppendChild(commentNode);
                    }
                    break;

                case OdfUtf8XmlTokenKind.ProcessingInstruction:
                    if (stack.Count > 0)
                    {
                        string piVal = token.GetValueString();
                        AddXmlCharacters(options, ref xmlCharacterCount, piVal.Length);
                        OdfNode instructionNode = new(OdfNodeType.ProcessingInstruction, token.GetNameString(), string.Empty)
                        {
                            TextContent = piVal
                        };
                        stack.Peek().AppendChild(instructionNode);
                    }
                    break;

                case OdfUtf8XmlTokenKind.Text:
                    if (stack.Count > 0)
                    {
                        string textVal = OdfUtf8XmlReader.GetStringMaybeDecoded(token.Value);
                        AddXmlCharacters(options, ref xmlCharacterCount, textVal.Length);
                        OdfNode textNode = new(OdfNodeType.Text, string.Empty, string.Empty)
                        {
                            TextContent = textVal
                        };
                        stack.Peek().AppendChild(textNode);
                    }
                    break;
            }
        }

        if (rootNode is null)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfXmlReader_InvalidNotFound"));
        }

        stopwatch.Stop();
        OdfPerformanceTelemetry.RecordXmlParse(stopwatch.Elapsed.TotalMilliseconds);
        return rootNode;
    }

    private static bool TryFindEndElementOffset(
        OdfUtf8XmlReader reader,
        string fullElementName,
        out int endOffset,
        out OdfUtf8XmlReader readerAtEnd)
    {
        int depth = 1;
        OdfUtf8XmlToken token;
        OdfUtf8XmlReader scanner = reader;
        while (scanner.Read(out token))
        {
            if (token.Kind == OdfUtf8XmlTokenKind.StartElement)
            {
                if (token.GetNameString() == fullElementName)
                {
                    depth++;
                }
            }
            else if (token.Kind == OdfUtf8XmlTokenKind.EndElement)
            {
                if (token.GetNameString() == fullElementName)
                {
                    depth--;
                    if (depth == 0)
                    {
                        endOffset = token.Offset;
                        readerAtEnd = scanner;
                        return true;
                    }
                }
            }
        }

        endOffset = reader.Position;
        readerAtEnd = reader;
        return false;
    }

    private static void AddNamespaceDeclarations(ReadOnlySpan<byte> attrsSpan, Dictionary<string, string> namespaces)
    {
        int i = 0;
        while (i < attrsSpan.Length)
        {
            while (i < attrsSpan.Length && IsWhitespace(attrsSpan[i]))
                i++;
            if (i >= attrsSpan.Length)
                break;

            int nameStart = i;
            while (i < attrsSpan.Length && attrsSpan[i] != (byte)'=' && !IsWhitespace(attrsSpan[i]))
                i++;
            ReadOnlySpan<byte> attrNameSpan = attrsSpan.Slice(nameStart, i - nameStart);

            while (i < attrsSpan.Length && attrsSpan[i] != (byte)'=')
                i++;
            if (i < attrsSpan.Length)
                i++;

            while (i < attrsSpan.Length && attrsSpan[i] != (byte)'"' && attrsSpan[i] != (byte)'\'')
                i++;
            if (i >= attrsSpan.Length)
                break;
            byte quote = attrsSpan[i];
            i++;

            int valueStart = i;
            while (i < attrsSpan.Length && attrsSpan[i] != quote)
                i++;
            ReadOnlySpan<byte> attrValueSpan = attrsSpan.Slice(valueStart, i - valueStart);
            if (i < attrsSpan.Length)
                i++;

            string attrFullName = Encoding.UTF8.GetString(
#if NETSTANDARD2_0
                attrNameSpan.ToArray()
#else
                attrNameSpan
#endif
            );

            if (attrFullName == "xmlns")
            {
                namespaces[string.Empty] = OdfUtf8XmlReader.GetStringMaybeDecoded(attrValueSpan);
            }
            else if (attrFullName.StartsWith("xmlns:", StringComparison.Ordinal))
            {
                namespaces[attrFullName.Substring("xmlns:".Length)] = OdfUtf8XmlReader.GetStringMaybeDecoded(attrValueSpan);
            }
        }
    }

    private static void ParseAttributes(
        ReadOnlySpan<byte> attrsSpan,
        OdfNode node,
        IReadOnlyDictionary<string, string> namespaces,
        ref long xmlCharacterCount,
        OdfLoadOptions options)
    {
        int i = 0;
        while (i < attrsSpan.Length)
        {
            while (i < attrsSpan.Length && IsWhitespace(attrsSpan[i]))
                i++;
            if (i >= attrsSpan.Length)
                break;

            int nameStart = i;
            while (i < attrsSpan.Length && attrsSpan[i] != (byte)'=' && !IsWhitespace(attrsSpan[i]))
                i++;
            var attrNameSpan = attrsSpan.Slice(nameStart, i - nameStart);

            while (i < attrsSpan.Length && attrsSpan[i] != (byte)'=')
                i++;
            if (i < attrsSpan.Length)
                i++; // consume '='

            while (i < attrsSpan.Length && attrsSpan[i] != (byte)'"' && attrsSpan[i] != (byte)'\'')
                i++;
            if (i >= attrsSpan.Length)
                break;
            byte quote = attrsSpan[i];
            i++; // consume quote

            int valStart = i;
            while (i < attrsSpan.Length && attrsSpan[i] != quote)
                i++;
            var attrValSpan = attrsSpan.Slice(valStart, i - valStart);
            if (i < attrsSpan.Length)
                i++; // consume quote

            string attrValue = OdfUtf8XmlReader.GetStringMaybeDecoded(attrValSpan);

            if (IsNamespaceDeclaration(attrNameSpan))
            {
                AddXmlCharacters(options, ref xmlCharacterCount, attrNameSpan.Length + attrValue.Length);
                continue;
            }

            ResolveAttributeName(attrNameSpan, namespaces, out string prefix, out string localName, out string nsUri);

            AddXmlCharacters(options, ref xmlCharacterCount, localName.Length + prefix.Length + nsUri.Length + attrValue.Length);
            node.SetAttribute(localName, nsUri, attrValue, prefix);
        }
    }

    private static void ResolveAttributeName(
        ReadOnlySpan<byte> name,
        IReadOnlyDictionary<string, string> namespaces,
        out string prefix,
        out string localName,
        out string namespaceUri)
    {
        if (OdfCustomAttributeRegistry.TryMatchLocalName(name, out string registeredLocalName))
        {
            ResolveQualifiedName(name, namespaces, out prefix, out localName, out namespaceUri);
            if (string.Equals(localName, registeredLocalName, StringComparison.Ordinal) &&
                OdfCustomAttributeRegistry.IsRegistered(localName, namespaceUri))
            {
                return;
            }
        }

        ResolveQualifiedName(name, namespaces, out prefix, out localName, out namespaceUri);
    }

    private static void ResolveQualifiedName(
        ReadOnlySpan<byte> name,
        IReadOnlyDictionary<string, string> namespaces,
        out string prefix,
        out string localName,
        out string namespaceUri)
    {
        if (OdfUtf8XmlReader.TryResolveKnownQualifiedName(name, namespaces, out prefix, out localName, out namespaceUri))
        {
            return;
        }

        string fullName = Encoding.UTF8.GetString(
#if NETSTANDARD2_0
            name.ToArray()
#else
            name
#endif
        );
        prefix = string.Empty;
        localName = fullName;
        int colonIdx = fullName.IndexOf(':');
        if (colonIdx >= 0)
        {
            prefix = fullName.Substring(0, colonIdx);
            localName = fullName.Substring(colonIdx + 1);
        }

        namespaceUri = prefix.Length == 0 ? string.Empty : LookupNamespaceUri(prefix, namespaces);
    }

    private static bool IsNamespaceDeclaration(ReadOnlySpan<byte> name)
    {
        return name.SequenceEqual("xmlns"u8) || name.StartsWith("xmlns:"u8);
    }

    private static string LookupNamespaceUri(string prefix, IReadOnlyDictionary<string, string> namespaces)
    {
        return namespaces.TryGetValue(prefix, out string? namespaceUri)
            ? namespaceUri
            : OdfNamespaces.LookupNamespaceUri(prefix);
    }

    private static bool IsWhitespace(byte b)
    {
        return b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n';
    }

    /// <summary>
    /// 將 ODF XML 檔案串流解析為記憶體 DOM 節點樹。
    /// </summary>
    /// <param name="stream">要解析的 XML 輸入串流</param>
    /// <param name="options">載入選項；如果為 <see langword="null"/>，則使用預設選項</param>
    /// <returns>解析完成的根元素節點</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="stream"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="SecurityException">當 XML 巢狀深度超過限制時擲出</exception>
    /// <exception cref="InvalidDataException">當 XML 結構無效（例如找不到根元素）時擲出</exception>
    public static OdfNode Parse(Stream stream, OdfLoadOptions? options = null)
    {
        var stopwatch = Stopwatch.StartNew();
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));
        options ??= OdfLoadOptions.Default;

        XmlReaderSettings settings = new()
        {
            NameTable = OdfXmlNameTable.Create(),
            DtdProcessing = DtdProcessing.Prohibit, // XXE 防禦
            XmlResolver = null,                     // XXE 防禦
            IgnoreWhitespace = false,               // 保留有意義的空白
            IgnoreComments = false,
            IgnoreProcessingInstructions = false,
            MaxCharactersInDocument = options.MaxXmlCharactersInDocument > 0
                ? options.MaxXmlCharactersInDocument
                : 0
        };

        OdfNode? rootNode = null;
        Stack<OdfNode> stack = new();
        int currentDepth = 0;
        long xmlCharacterCount = 0;

        using (var reader = XmlReader.Create(stream, settings))
        {
            try
            {
                bool readNext = true;
                while (readNext ? reader.Read() : (readNext = true))
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            currentDepth++;
                            // 堆疊溢位 DoS 防禦保護
                            if (currentDepth > MaxElementDepth)
                            {
                                throw new SecurityException(OdfLocalizer.GetMessage("Err_OdfXmlReader_XmlElementNestingDepth", CultureInfo.InvariantCulture, currentDepth, MaxElementDepth));
                            }

                            string localName = OdfXmlStringPools.GetOrAdd(reader.LocalName);
                            string nsUri = OdfXmlStringPools.GetOrAdd(reader.NamespaceURI);
                            string prefix = OdfXmlStringPools.GetOrAdd(reader.Prefix);
                            AddXmlCharacters(options, ref xmlCharacterCount, localName.Length + nsUri.Length + prefix.Length);

                            var node = OdfNodeFactory.CreateElement(localName, nsUri, prefix);

                            // 解析屬性
                            if (reader.HasAttributes)
                            {
                                while (reader.MoveToNextAttribute())
                                {
                                    string attrLocalName = OdfXmlStringPools.GetOrAdd(reader.LocalName);
                                    string attrNsUri = OdfXmlStringPools.GetOrAdd(reader.NamespaceURI);
                                    string attrPrefix = OdfXmlStringPools.GetOrAdd(reader.Prefix);
                                    string attrValue = reader.Value; // 一般不在 StringPool 中快取屬性值，因為其值變化較大，除非是空值或布林值
                                    AddXmlCharacters(
                                        options,
                                        ref xmlCharacterCount,
                                        attrLocalName.Length + attrNsUri.Length + attrPrefix.Length + attrValue.Length);

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

                            bool shouldLazy = false;
                            if (options.AllowLazyLoading && !reader.IsEmptyElement)
                            {
                                if (localName == "table" && nsUri == "urn:oasis:names:tc:opendocument:xmlns:table:1.0")
                                {
                                    string innerXml = reader.ReadInnerXml();
                                    node._lazyXmlMemory = Encoding.UTF8.GetBytes(innerXml);
                                    node._isLazy = true;
                                    shouldLazy = true;
                                    currentDepth--;
                                    readNext = false;
                                }
                                else if (localName == "meta" || localName == "settings" || localName == "styles" || localName == "p" || localName == "list")
                                {
                                    string innerXml = reader.ReadInnerXml();
                                    if (innerXml.Length >= 8192)
                                    {
                                        node._lazyXmlMemory = Encoding.UTF8.GetBytes(innerXml);
                                        node._isLazy = true;
                                        shouldLazy = true;
                                    }
                                    else if (innerXml.Length > 0)
                                    {
                                        byte[] innerBytes = Encoding.UTF8.GetBytes("<wrapper" +
                                            " xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"" +
                                            " xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"" +
                                            " xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\"" +
                                            " xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\"" +
                                            " xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\"" +
                                            " xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\"" +
                                            " xmlns:xlink=\"http://www.w3.org/1999/xlink\"" +
                                            ">" + innerXml + "</wrapper>");
                                        using var tempMs = new MemoryStream(innerBytes);
                                        OdfNode? tempRoot = OdfXmlReader.Parse(tempMs, new OdfLoadOptions { AllowLazyLoading = false });
                                        if (tempRoot is not null)
                                        {
                                            OdfNode? nextChild = tempRoot.FirstChild;
                                            while (nextChild is not null)
                                            {
                                                OdfNode? sibling = nextChild.NextSibling;
                                                tempRoot.Children.Remove(nextChild);
                                                node.AppendChild(nextChild);
                                                nextChild = sibling;
                                            }
                                        }
                                    }
                                    currentDepth--;
                                    readNext = false;
                                }
                            }

                            if (!shouldLazy)
                            {
                                if (!reader.IsEmptyElement && readNext)
                                {
                                    stack.Push(node);
                                }
                                else if (reader.IsEmptyElement)
                                {
                                    currentDepth--;
                                }
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
                                AddXmlCharacters(options, ref xmlCharacterCount, commentVal.Length);
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
                                AddXmlCharacters(options, ref xmlCharacterCount, reader.Name.Length + reader.Value.Length);
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
                                AddXmlCharacters(options, ref xmlCharacterCount, textVal.Length);
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
                if (IsXmlSizeLimitExceeded(ex))
                {
                    throw new SecurityException(
                        OdfLocalizer.GetMessage("Err_OdfXmlReader_XmlCharacterLimitExceeded", CultureInfo.InvariantCulture, options.MaxXmlCharactersInDocument),
                        ex);
                }

                if (IsDtdException(ex))
                {
                    throw;
                }

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
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfXmlReader_InvalidNotFound"));
        }

        stopwatch.Stop();
        OdfPerformanceTelemetry.RecordXmlParse(stopwatch.Elapsed.TotalMilliseconds);

        return rootNode;
    }

    private static void AddXmlCharacters(OdfLoadOptions options, ref long total, long count)
    {
        if (options.MaxXmlCharactersInDocument <= 0 || count <= 0)
        {
            return;
        }

        total += count;
        if (total > options.MaxXmlCharactersInDocument)
        {
            throw new SecurityException(
                OdfLocalizer.GetMessage("Err_OdfXmlReader_XmlCharacterLimitExceeded", CultureInfo.InvariantCulture, $"{total} > {options.MaxXmlCharactersInDocument}"));
        }
    }

    private static bool IsXmlSizeLimitExceeded(XmlException exception)
    {
        return exception.Message.IndexOf(nameof(XmlReaderSettings.MaxCharactersInDocument), StringComparison.OrdinalIgnoreCase) >= 0 ||
            exception.Message.IndexOf("maximum number of characters", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsDtdException(XmlException exception)
    {
        // 檢查例外訊息中是否包含 DTD 相關關鍵字
        return exception.Message.Contains("DTD", StringComparison.OrdinalIgnoreCase);
    }
}
