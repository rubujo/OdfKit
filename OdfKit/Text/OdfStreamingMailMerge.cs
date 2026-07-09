using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Text;

/// <summary>
/// Provides a SAX-style streaming mail-merge and template fill engine with low residency design.
/// 提供以 SAX 風格串流為主、低常駐記憶體設計的郵件合併與範本套印引擎。
/// </summary>
public static partial class OdfStreamingMailMerge
{
    /// <summary>
    /// Asynchronously applies template data merging and writes the result to the target stream.
    /// 非同步套用範本資料合併，並將結果輸出至目標串流。
    /// </summary>
    public static Task ApplyTemplateAsync(Stream templateStream, Stream outputStream, IDictionary<string, object?> data) => ApplyTemplateAsync(templateStream, outputStream, data, default);

    /// <summary>
    /// Full overload of ApplyTemplateAsync that accepts templateStream, outputStream, and cancellationToken.
    /// ApplyTemplateAsync 完整多載：接受 templateStream、outputStream 與 cancellationToken。
    /// </summary>
    public static async Task ApplyTemplateAsync(
        Stream templateStream,
        Stream outputStream,
        IDictionary<string, object?> data,
        CancellationToken cancellationToken)
    {
        if (templateStream is null)
            throw new ArgumentNullException(nameof(templateStream));
        if (outputStream is null)
            throw new ArgumentNullException(nameof(outputStream));
        if (data is null)
            throw new ArgumentNullException(nameof(data));

        cancellationToken.ThrowIfCancellationRequested();

        using var sourceZip = new ZipArchive(templateStream, ZipArchiveMode.Read, leaveOpen: true);
        using var destZip = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true);
        OdfLoadOptions loadOptions = OdfLoadOptions.Default;
        ValidateEntryCount(sourceZip, loadOptions);
        long totalUncompressedSize = 0;

        foreach (ZipArchiveEntry entry in sourceZip.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string entryName = ValidateSourceEntry(entry, loadOptions, ref totalUncompressedSize);

            if (entryName.EndsWith("content.xml", StringComparison.OrdinalIgnoreCase) ||
                entryName.EndsWith("styles.xml", StringComparison.OrdinalIgnoreCase))
            {
                ZipArchiveEntry newEntry = destZip.CreateEntry(entryName, CompressionLevel.Optimal);
                using Stream srcStream = entry.Open();
                using Stream destStream = newEntry.Open();

                await ProcessXmlTemplateAsync(srcStream, destStream, data, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                ZipArchiveEntry newEntry = destZip.CreateEntry(entryName, CompressionLevel.NoCompression);
                using Stream srcStream = entry.Open();
                using Stream destStream = newEntry.Open();

                await OdfBoundedStreamReader.CopyToAsync(
                    srcStream,
                    destStream,
                    loadOptions.MaxEntrySize,
                    "Err_OdfPackage_InputStreamSizeLimitExceeded",
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Applies batch template async.
    /// 非同步套用批次範本資料合併，並將結果輸出至目標串流。
    /// </summary>
    public static Task ApplyBatchTemplateAsync(Stream templateStream, Stream outputStream, IEnumerable<IDictionary<string, object?>> dataSequence) => ApplyBatchTemplateAsync(templateStream, outputStream, dataSequence, default);

    /// <summary>
    /// Full overload of ApplyBatchTemplateAsync that accepts templateStream, outputStream, and cancellationToken.
    /// ApplyBatchTemplateAsync 完整多載：接受 templateStream、outputStream 與 cancellationToken。
    /// </summary>
    public static Task ApplyBatchTemplateAsync(
        Stream templateStream,
        Stream outputStream,
        IEnumerable<IDictionary<string, object?>> dataSequence,
        CancellationToken cancellationToken)
    {
        if (templateStream is null)
            throw new ArgumentNullException(nameof(templateStream));
        if (outputStream is null)
            throw new ArgumentNullException(nameof(outputStream));
        if (dataSequence is null)
            throw new ArgumentNullException(nameof(dataSequence));

        return ApplyBatchTemplateCoreAsync(
            templateStream,
            outputStream,
            WrapAsAsyncEnumerable(dataSequence),
            cancellationToken);
    }

    /// <summary>
    /// Applies batch template async.
    /// 非同步套用批次範本資料合併，資料序列以非同步方式列舉，並將結果輸出至目標串流。
    /// </summary>
    public static Task ApplyBatchTemplateAsync(Stream templateStream, Stream outputStream, IAsyncEnumerable<IDictionary<string, object?>> dataSequence) => ApplyBatchTemplateAsync(templateStream, outputStream, dataSequence, default);

    /// <summary>
    /// Full overload of ApplyBatchTemplateAsync that accepts templateStream, outputStream, and cancellationToken.
    /// ApplyBatchTemplateAsync 完整多載：接受 templateStream、outputStream 與 cancellationToken。
    /// </summary>
    public static Task ApplyBatchTemplateAsync(
        Stream templateStream,
        Stream outputStream,
        IAsyncEnumerable<IDictionary<string, object?>> dataSequence,
        CancellationToken cancellationToken)
    {
        if (templateStream is null)
            throw new ArgumentNullException(nameof(templateStream));
        if (outputStream is null)
            throw new ArgumentNullException(nameof(outputStream));
        if (dataSequence is null)
            throw new ArgumentNullException(nameof(dataSequence));

        return ApplyBatchTemplateCoreAsync(templateStream, outputStream, dataSequence, cancellationToken);
    }

    private static async Task ApplyBatchTemplateCoreAsync(
        Stream templateStream,
        Stream outputStream,
        IAsyncEnumerable<IDictionary<string, object?>> dataSequence,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 單向資料源（如 DbDataReader）只能列舉一次：
        // 先預取首筆供 styles.xml 套印使用，content.xml 批次合併時改以可重播首筆的序列走訪，
        // 避免同一序列被列舉兩次而遺失第一筆記錄。
        IAsyncEnumerator<IDictionary<string, object?>> enumerator = dataSequence.GetAsyncEnumerator(cancellationToken);
        try
        {
            bool hasFirst = await enumerator.MoveNextAsync().ConfigureAwait(false);
            IDictionary<string, object?> firstData = hasFirst
                ? enumerator.Current
                : new Dictionary<string, object?>();

            using var sourceZip = new ZipArchive(templateStream, ZipArchiveMode.Read, leaveOpen: true);
            using var destZip = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true);
            OdfLoadOptions loadOptions = OdfLoadOptions.Default;
            ValidateEntryCount(sourceZip, loadOptions);
            long totalUncompressedSize = 0;

            bool replaySequenceUsed = false;
            foreach (ZipArchiveEntry entry in sourceZip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string entryName = ValidateSourceEntry(entry, loadOptions, ref totalUncompressedSize);

                if (entryName.EndsWith("content.xml", StringComparison.OrdinalIgnoreCase) ||
                    entryName.EndsWith("styles.xml", StringComparison.OrdinalIgnoreCase))
                {
                    ZipArchiveEntry newEntry = destZip.CreateEntry(entryName, CompressionLevel.Optimal);
                    using Stream srcStream = entry.Open();
                    using Stream destStream = newEntry.Open();

                    if (entryName.EndsWith("content.xml", StringComparison.OrdinalIgnoreCase))
                    {
                        // 重播序列只能走訪一次；範本含多個 content.xml（如內嵌物件的
                        // Object 1/content.xml）時，後續項目改以來源序列重新列舉——
                        // 可重複列舉的來源（清單、陣列）可完整重播，單向來源則盡力而為。
                        IAsyncEnumerable<IDictionary<string, object?>> rows;
                        if (!replaySequenceUsed)
                        {
                            rows = ReplayFirstThenRest(firstData, hasFirst, enumerator);
                            replaySequenceUsed = true;
                        }
                        else
                        {
                            rows = dataSequence;
                        }

                        await ProcessXmlTemplateBatchAsync(
                            srcStream,
                            destStream,
                            rows,
                            cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await ProcessXmlTemplateAsync(srcStream, destStream, firstData, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    ZipArchiveEntry newEntry = destZip.CreateEntry(entryName, CompressionLevel.NoCompression);
                    using Stream srcStream = entry.Open();
                    using Stream destStream = newEntry.Open();

                    await OdfBoundedStreamReader.CopyToAsync(
                        srcStream,
                        destStream,
                        loadOptions.MaxEntrySize,
                        "Err_OdfPackage_InputStreamSizeLimitExceeded",
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static void ValidateEntryCount(ZipArchive sourceZip, OdfLoadOptions loadOptions)
    {
        if (sourceZip.Entries.Count > loadOptions.MaxZipEntries)
        {
            throw new SecurityException(
                OdfLocalizer.GetMessage("Err_OdfPackage_ZipEntryCountLimitExceeded", sourceZip.Entries.Count, loadOptions.MaxZipEntries));
        }
    }

    private static string ValidateSourceEntry(
        ZipArchiveEntry entry,
        OdfLoadOptions loadOptions,
        ref long totalUncompressedSize)
    {
        string entryName = OdfPackage.SanitizeEntryName(entry.FullName);
        if (entry.Length > loadOptions.MaxEntrySize)
        {
            throw new SecurityException(
                OdfLocalizer.GetMessage("Err_OdfPackage_ZipEntrySizeLimitExceeded", entryName, entry.Length, loadOptions.MaxEntrySize));
        }

        totalUncompressedSize = OdfBoundedStreamReader.AddBytes(
            totalUncompressedSize,
            entry.Length,
            loadOptions.MaxTotalUncompressedSize,
            "Err_OdfPackage_ZipTotalUncompressedSizeLimitExceeded");

        return entryName;
    }

    private static async IAsyncEnumerable<IDictionary<string, object?>> ReplayFirstThenRest(
        IDictionary<string, object?> firstData,
        bool hasFirst,
        IAsyncEnumerator<IDictionary<string, object?>> enumerator)
    {
        if (!hasFirst)
        {
            yield break;
        }

        yield return firstData;
        while (await enumerator.MoveNextAsync().ConfigureAwait(false))
        {
            yield return enumerator.Current;
        }
    }

    private static async IAsyncEnumerable<IDictionary<string, object?>> WrapAsAsyncEnumerable(
        IEnumerable<IDictionary<string, object?>> source)
    {
        foreach (IDictionary<string, object?> row in source)
        {
            yield return row;
        }

        // 滿足 async 迭代器的編譯需求（同步來源沒有實際的非同步等待點）
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Applies batch template async.
    /// 非同步套用批次範本資料合併，支援 DbDataReader，並將結果輸出至目標串流。
    /// </summary>
    public static Task ApplyBatchTemplateAsync(Stream templateStream, Stream outputStream, DbDataReader reader) => ApplyBatchTemplateAsync(templateStream, outputStream, reader, default);

    /// <summary>
    /// Full overload of ApplyBatchTemplateAsync that accepts templateStream, outputStream, reader, and cancellationToken.
    /// ApplyBatchTemplateAsync 完整多載：接受 templateStream、outputStream、reader 與 cancellationToken。
    /// </summary>
    public static Task ApplyBatchTemplateAsync(
        Stream templateStream,
        Stream outputStream,
        DbDataReader reader,
        CancellationToken cancellationToken)
    {
        if (reader is null)
            throw new ArgumentNullException(nameof(reader));

        var sequence = AsAsyncEnumerable(reader, default);
        return ApplyBatchTemplateAsync(templateStream, outputStream, sequence, cancellationToken);
    }

    private static async IAsyncEnumerable<IDictionary<string, object?>> AsAsyncEnumerable(
        DbDataReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 以 ReadAsync 取代同步 Read：資料庫往返不再阻塞執行緒，
        // 非同步批次套印的吞吐量不受同步 I/O 等待拖累。
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            yield return row;
        }
    }

    private static void FlushStaticSegment(MemoryStream ms, List<TemplateSegment> segments)
    {
        if (ms.Length > 0)
        {
            byte[] bytes = ms.ToArray();
            segments.Add(new StaticSegment(bytes));
            ms.SetLength(0);
        }
    }

    private static List<TemplateSegment> CompileSegments(XmlReader reader, string? endMarker, out string remainingTextSuffix)
    {
        remainingTextSuffix = "";
        var segments = new List<TemplateSegment>();
        var staticMs = new MemoryStream();

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                var sb = new StringBuilder();
                sb.Append('<');
                if (!string.IsNullOrEmpty(reader.Prefix))
                {
                    sb.Append(reader.Prefix);
                    sb.Append(':');
                }
                sb.Append(reader.LocalName);

                // 如果是 office:document-content 或 office:document-styles 根節點，補齊常用 namespace 宣告
                bool isRoot = reader.Prefix == "office" && (reader.LocalName == "document-content" || reader.LocalName == "document-styles");
                var declaredPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (reader.MoveToFirstAttribute())
                {
                    do
                    {
                        if (reader.Prefix == "xmlns")
                        {
                            declaredPrefixes.Add(reader.LocalName);
                        }
                        else if (string.IsNullOrEmpty(reader.Prefix) && reader.LocalName == "xmlns")
                        {
                            declaredPrefixes.Add(string.Empty);
                        }
                    } while (reader.MoveToNextAttribute());
                    reader.MoveToElement();
                }

                if (reader.MoveToFirstAttribute())
                {
                    do
                    {
                        sb.Append(' ');
                        if (!string.IsNullOrEmpty(reader.Prefix))
                        {
                            sb.Append(reader.Prefix);
                            sb.Append(':');
                        }
                        sb.Append(reader.LocalName);
                        sb.Append("=\"");
                        sb.Append(EscapeXmlAttribute(reader.Value));
                        sb.Append('"');
                    } while (reader.MoveToNextAttribute());
                    reader.MoveToElement();
                }

                if (isRoot)
                {
                    var commonNamespaces = new (string Prefix, string Uri)[]
                    {
                        ("style", "urn:oasis:names:tc:opendocument:xmlns:style:1.0"),
                        ("fo", "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"),
                        ("svg", "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0"),
                        ("table", "urn:oasis:names:tc:opendocument:xmlns:table:1.0"),
                        ("draw", "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0"),
                        ("xlink", "http://www.w3.org/1999/xlink"),
                        ("number", "urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0"),
                        ("chart", "urn:oasis:names:tc:opendocument:xmlns:chart:1.0"),
                        ("calcext", "urn:org:documentfoundation:names:experimental:calc:xmlns:calcext:1.0"),
                        ("loext", "urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0"),
                        ("dc", "http://purl.org/dc/elements/1.1/"),
                        ("meta", "urn:oasis:names:tc:opendocument:xmlns:meta:1.0"),
                        ("config", "urn:oasis:names:tc:opendocument:xmlns:config:1.0"),
                        ("script", "urn:oasis:names:tc:opendocument:xmlns:script:1.0"),
                        ("anim", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0"),
                        ("smil", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0"),
                        ("form", "urn:oasis:names:tc:opendocument:xmlns:form:1.0"),
                        ("pkg", "http://docs.oasis-open.org/opendocument/meta/pkg#"),
                        ("of", "urn:oasis:names:tc:opendocument:xmlns:of:1.2"),
                        ("oooc", "http://openoffice.org/2004/calc"),
                        ("math", "http://www.w3.org/1998/Math/MathML"),
                        ("dr3d", "urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0"),
                        ("db", "urn:oasis:names:tc:opendocument:xmlns:database:1.0"),
                        ("report", "urn:oasis:names:tc:opendocument:xmlns:report:1.0")
                    };

                    foreach (var ns in commonNamespaces)
                    {
                        if (!declaredPrefixes.Contains(ns.Prefix))
                        {
                            sb.Append(" xmlns:");
                            sb.Append(ns.Prefix);
                            sb.Append("=\"");
                            sb.Append(ns.Uri);
                            sb.Append('"');
                        }
                    }
                }

                if (reader.IsEmptyElement)
                {
                    sb.Append("/>");
                }
                else
                {
                    sb.Append('>');
                }
                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                staticMs.Write(bytes, 0, bytes.Length);
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                var sb = new StringBuilder();
                sb.Append("</");
                if (!string.IsNullOrEmpty(reader.Prefix))
                {
                    sb.Append(reader.Prefix);
                    sb.Append(':');
                }
                sb.Append(reader.LocalName);
                sb.Append('>');
                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                staticMs.Write(bytes, 0, bytes.Length);
            }
            else if (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.Whitespace || reader.NodeType == XmlNodeType.SignificantWhitespace)
            {
                string val = reader.Value;

                // 檢查結束標記
                if (endMarker != null && val.Contains(endMarker))
                {
                    int endIdx = val.IndexOf(endMarker, StringComparison.Ordinal);
                    if (endIdx > 0)
                    {
                        string prefix = val.Substring(0, endIdx);
                        ParseTextOnly(prefix, segments, staticMs);
                    }
                    FlushStaticSegment(staticMs, segments);
                    if (endIdx + endMarker.Length < val.Length)
                    {
                        remainingTextSuffix = val.Substring(endIdx + endMarker.Length);
                    }
                    return segments;
                }

                // 解析文字以尋找 #foreach 與佔位符
                ParseTextWithForeach(val, reader, segments, staticMs);
            }
            else if (reader.NodeType == XmlNodeType.Comment)
            {
                string comment = "<!--" + reader.Value + "-->";
                byte[] bytes = Encoding.UTF8.GetBytes(comment);
                staticMs.Write(bytes, 0, bytes.Length);
            }
            else if (reader.NodeType == XmlNodeType.ProcessingInstruction)
            {
                string pi = "<?" + reader.Name + " " + reader.Value + "?>";
                byte[] bytes = Encoding.UTF8.GetBytes(pi);
                staticMs.Write(bytes, 0, bytes.Length);
            }
        }

        FlushStaticSegment(staticMs, segments);
        return segments;
    }

    private static void ParseTextWithForeach(string text, XmlReader reader, List<TemplateSegment> segments, MemoryStream ms)
    {
        int pos = 0;
        while (pos < text.Length)
        {
            int braceIdx = text.IndexOf('{', pos);
            int bracketIdx = text.IndexOf('[', pos);

            int start = -1;
            int end = -1;
            bool isCurly = false;

            if (braceIdx >= 0 && (bracketIdx < 0 || braceIdx < bracketIdx))
            {
                if (braceIdx + 1 < text.Length && text[braceIdx + 1] == '{')
                {
                    int endCurly = text.IndexOf("}}", braceIdx + 2, StringComparison.Ordinal);
                    if (endCurly >= 0)
                    {
                        start = braceIdx;
                        end = endCurly + 2;
                        isCurly = true;
                    }
                }
            }

            if (start == -1 && bracketIdx >= 0)
            {
                int endBracket = text.IndexOf(']', bracketIdx + 1);
                if (endBracket >= 0)
                {
                    start = bracketIdx;
                    end = endBracket + 1;
                    isCurly = false;
                }
            }

            if (start >= 0)
            {
                if (start > pos)
                {
                    WriteEscapedTextToStream(text.Substring(pos, start - pos), ms);
                }

                string field = isCurly
                    ? text.Substring(start + 2, end - start - 4).Trim()
                    : text.Substring(start + 1, end - start - 2).Trim();

                if (field.StartsWith("#foreach "))
                {
                    FlushStaticSegment(ms, segments);

                    // 解析 foreach 宣告
                    string decl = field.Substring("#foreach ".Length).Trim();
                    var parts = decl.Split(new[] { " in " }, StringSplitOptions.None);
                    string itemName = "";
                    string collectionName = "";
                    if (parts.Length == 2)
                    {
                        itemName = parts[0].Trim();
                        collectionName = parts[1].Trim();
                    }

                    // 遞迴編譯迴圈主體
                    var bodySegments = CompileSegments(reader, "[/foreach]", out string remainingText);

                    segments.Add(new ForeachSegment(itemName, collectionName, bodySegments));

                    // 處理 [/foreach] 之後的殘餘文字
                    if (!string.IsNullOrEmpty(remainingText))
                    {
                        text = remainingText;
                        pos = 0;
                        continue;
                    }
                    pos = text.Length;
                }
                else if (field.StartsWith("/foreach"))
                {
                    pos = end;
                }
                else
                {
                    FlushStaticSegment(ms, segments);
                    segments.Add(new PlaceholderSegment(field));
                    pos = end;
                }
            }
            else
            {
                WriteEscapedTextToStream(text.Substring(pos), ms);
                break;
            }
        }
    }

    private static void ParseTextOnly(string text, List<TemplateSegment> segments, MemoryStream ms)
    {
        int pos = 0;
        while (pos < text.Length)
        {
            int braceIdx = text.IndexOf('{', pos);
            int bracketIdx = text.IndexOf('[', pos);

            int start = -1;
            int end = -1;
            bool isCurly = false;

            if (braceIdx >= 0 && (bracketIdx < 0 || braceIdx < bracketIdx))
            {
                if (braceIdx + 1 < text.Length && text[braceIdx + 1] == '{')
                {
                    int endCurly = text.IndexOf("}}", braceIdx + 2, StringComparison.Ordinal);
                    if (endCurly >= 0)
                    {
                        start = braceIdx;
                        end = endCurly + 2;
                        isCurly = true;
                    }
                }
            }

            if (start == -1 && bracketIdx >= 0)
            {
                int endBracket = text.IndexOf(']', bracketIdx + 1);
                if (endBracket >= 0)
                {
                    start = bracketIdx;
                    end = endBracket + 1;
                    isCurly = false;
                }
            }

            if (start >= 0)
            {
                if (start > pos)
                {
                    WriteEscapedTextToStream(text.Substring(pos, start - pos), ms);
                }

                string field = isCurly
                    ? text.Substring(start + 2, end - start - 4).Trim()
                    : text.Substring(start + 1, end - start - 2).Trim();

                FlushStaticSegment(ms, segments);
                segments.Add(new PlaceholderSegment(field));
                pos = end;
            }
            else
            {
                WriteEscapedTextToStream(text.Substring(pos), ms);
                break;
            }
        }
    }

    private static PrecompiledBatchTemplate CompileBatchTemplate(List<XmlNodeInfo> nodes)
    {
        // 尋找 bodyStartIdx 與 bodyEndIdx
        int bodyStartIdx = -1;
        int bodyEndIdx = -1;
        string bodyStartLocalName = "text";
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node.NodeType == XmlNodeType.Element &&
                (node.LocalName == "text" || node.LocalName == "spreadsheet") &&
                node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:office:1.0")
            {
                bodyStartIdx = i;
                bodyStartLocalName = node.LocalName;
                break;
            }
        }

        if (bodyStartIdx >= 0)
        {
            for (int i = bodyStartIdx + 1; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node.NodeType == XmlNodeType.EndElement &&
                    node.LocalName == bodyStartLocalName &&
                    node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:office:1.0")
                {
                    bodyEndIdx = i;
                    break;
                }
            }
        }

        // 若未尋得主體起點或終點，則將整體視為單一頁首處理
        if (bodyStartIdx < 0 || bodyEndIdx < 0)
        {
            var allSegments = CompileNodesToSegments(nodes, 0, nodes.Count);
            return new PrecompiledBatchTemplate(allSegments, new List<TemplateSegment>(), new List<TemplateSegment>());
        }

        // 準備頁首節點
        var headerNodes = new List<XmlNodeInfo>();
        for (int i = 0; i <= bodyStartIdx; i++)
        {
            headerNodes.Add(nodes[i]);
        }

        int autoStylesEndIdx = -1;
        for (int i = 0; i < headerNodes.Count; i++)
        {
            if (headerNodes[i].NodeType == XmlNodeType.EndElement &&
                headerNodes[i].LocalName == "automatic-styles" &&
                headerNodes[i].NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:office:1.0")
            {
                autoStylesEndIdx = i;
                break;
            }
        }

        var pageBreakStyleNode = new XmlNodeInfo
        {
            NodeType = XmlNodeType.Element,
            LocalName = "style",
            Prefix = "style",
            NamespaceUri = "urn:oasis:names:tc:opendocument:xmlns:style:1.0",
            Attributes = new List<XmlAttributeInfo>
            {
                new XmlAttributeInfo { LocalName = "name", Prefix = "style", NamespaceUri = "urn:oasis:names:tc:opendocument:xmlns:style:1.0", Value = "OdfKitPageBreak" },
                new XmlAttributeInfo { LocalName = "family", Prefix = "style", NamespaceUri = "urn:oasis:names:tc:opendocument:xmlns:style:1.0", Value = "paragraph" },
                new XmlAttributeInfo { LocalName = "parent-style-name", Prefix = "style", NamespaceUri = "urn:oasis:names:tc:opendocument:xmlns:style:1.0", Value = "Standard" }
            }
        };
        var pageBreakPropertiesNode = new XmlNodeInfo
        {
            NodeType = XmlNodeType.Element,
            LocalName = "paragraph-properties",
            Prefix = "style",
            NamespaceUri = "urn:oasis:names:tc:opendocument:xmlns:style:1.0",
            Attributes = new List<XmlAttributeInfo>
            {
                new XmlAttributeInfo { LocalName = "break-before", Prefix = "fo", NamespaceUri = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0", Value = "page" }
            },
            IsEmpty = true
        };
        var pageBreakStyleEndNode = new XmlNodeInfo
        {
            NodeType = XmlNodeType.EndElement,
            LocalName = "style",
            Prefix = "style",
            NamespaceUri = "urn:oasis:names:tc:opendocument:xmlns:style:1.0"
        };

        if (autoStylesEndIdx >= 0)
        {
            headerNodes.Insert(autoStylesEndIdx, pageBreakStyleEndNode);
            headerNodes.Insert(autoStylesEndIdx, pageBreakPropertiesNode);
            headerNodes.Insert(autoStylesEndIdx, pageBreakStyleNode);
        }
        else
        {
            int insertIdx = bodyStartIdx;
            headerNodes.Insert(insertIdx, new XmlNodeInfo { NodeType = XmlNodeType.EndElement, LocalName = "automatic-styles", Prefix = "office", NamespaceUri = "urn:oasis:names:tc:opendocument:xmlns:office:1.0" });
            headerNodes.Insert(insertIdx, pageBreakStyleEndNode);
            headerNodes.Insert(insertIdx, pageBreakPropertiesNode);
            headerNodes.Insert(insertIdx, pageBreakStyleNode);
            headerNodes.Insert(insertIdx, new XmlNodeInfo { NodeType = XmlNodeType.Element, LocalName = "automatic-styles", Prefix = "office", NamespaceUri = "urn:oasis:names:tc:opendocument:xmlns:office:1.0" });
        }

        var headerSegments = CompileNodesToSegments(headerNodes, 0, headerNodes.Count);

        // 準備內容節點
        var bodyNodes = new List<XmlNodeInfo>();
        for (int i = bodyStartIdx + 1; i < bodyEndIdx; i++)
        {
            bodyNodes.Add(nodes[i]);
        }
        var bodySegments = CompileNodesToSegments(bodyNodes, 0, bodyNodes.Count);

        // 準備頁尾節點
        var footerNodes = new List<XmlNodeInfo>();
        for (int i = bodyEndIdx; i < nodes.Count; i++)
        {
            footerNodes.Add(nodes[i]);
        }
        var footerSegments = CompileNodesToSegments(footerNodes, 0, footerNodes.Count);

        return new PrecompiledBatchTemplate(headerSegments, bodySegments, footerSegments);
    }

    private static List<TemplateSegment> CompileNodesToSegments(List<XmlNodeInfo> nodes, int startIdx, int endIdx)
    {
        var segments = new List<TemplateSegment>();
        var staticMs = new MemoryStream();

        int i = startIdx;
        while (i < endIdx)
        {
            var node = nodes[i];
            if (node.NodeType == XmlNodeType.Element || node.NodeType == XmlNodeType.EndElement || node.NodeType == XmlNodeType.Comment || node.NodeType == XmlNodeType.ProcessingInstruction)
            {
                SerializeNodeToStream(node, staticMs);
            }
            else if (node.NodeType == XmlNodeType.Text || node.NodeType == XmlNodeType.Whitespace || node.NodeType == XmlNodeType.SignificantWhitespace)
            {
                int foreachIdx = node.Value.IndexOf("[#foreach ", StringComparison.Ordinal);
                if (foreachIdx >= 0)
                {
                    int declStart = foreachIdx + "[#foreach ".Length;
                    int declEnd = node.Value.IndexOf(']', declStart);
                    if (declEnd >= 0)
                    {
                        string decl = node.Value.Substring(declStart, declEnd - declStart);
                        var parts = decl.Split(new[] { " in " }, StringSplitOptions.None);
                        if (parts.Length == 2)
                        {
                            string nestedItemName = parts[0].Trim();
                            string nestedCollectionName = parts[1].Trim();

                            int depth = 1;
                            int j = i + 1;
                            int nestedEndIdx = -1;
                            while (j < endIdx)
                            {
                                if (nodes[j].NodeType == XmlNodeType.Text && nodes[j].Value.Contains("[#foreach "))
                                    depth++;
                                else if (nodes[j].NodeType == XmlNodeType.Text && nodes[j].Value.Contains("[/foreach]"))
                                {
                                    depth--;
                                    if (depth == 0)
                                    {
                                        nestedEndIdx = j;
                                        break;
                                    }
                                }
                                j++;
                            }

                            if (nestedEndIdx >= 0)
                            {
                                if (foreachIdx > 0)
                                {
                                    string prefixText = node.Value.Substring(0, foreachIdx);
                                    ParseTextOnly(prefixText, segments, staticMs);
                                }

                                FlushStaticSegment(staticMs, segments);

                                // 遞迴編譯巢狀緩衝區
                                var nestedSegments = CompileNodesToSegments(nodes, i + 1, nestedEndIdx);
                                segments.Add(new ForeachSegment(nestedItemName, nestedCollectionName, nestedSegments));

                                // 處理結束節點中 [/foreach] 之後的任何文字
                                var endNode = nodes[nestedEndIdx];
                                int closeBraceIdx = endNode.Value.IndexOf("[/foreach]", StringComparison.Ordinal);
                                if (closeBraceIdx >= 0 && closeBraceIdx + "[/foreach]".Length < endNode.Value.Length)
                                {
                                    string suffixText = endNode.Value.Substring(closeBraceIdx + "[/foreach]".Length);
                                    ParseTextOnly(suffixText, segments, staticMs);
                                }

                                i = nestedEndIdx;
                            }
                        }
                    }
                }
                else
                {
                    ParseTextOnly(node.Value, segments, staticMs);
                }
            }
            i++;
        }

        FlushStaticSegment(staticMs, segments);
        return segments;
    }

    private static async Task ProcessXmlTemplateAsync(
        Stream sourceXml,
        Stream destXml,
        IDictionary<string, object?> data,
        CancellationToken cancellationToken)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        using var reader = XmlReader.Create(sourceXml, settings);

        // 進行一次性編譯
        var segments = CompileSegments(reader, null, out _);

        // 執行套印
        var localContext = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in segments)
        {
            await segment.WriteToAsync(destXml, data, localContext, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ProcessXmlTemplateBatchAsync(
        Stream sourceXml,
        Stream destXml,
        IAsyncEnumerable<IDictionary<string, object?>> dataSequence,
        CancellationToken cancellationToken)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        using var reader = XmlReader.Create(sourceXml, settings);

        // 將所有節點讀入暫存清單以進行一次性編譯
        var nodes = new List<XmlNodeInfo>();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var node = new XmlNodeInfo
            {
                NodeType = reader.NodeType,
                Name = reader.Name,
                LocalName = reader.LocalName,
                Prefix = reader.Prefix,
                NamespaceUri = reader.NamespaceURI,
                Value = reader.Value,
                IsEmpty = reader.NodeType == XmlNodeType.Element && reader.IsEmptyElement
            };
            if (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.MoveToFirstAttribute())
                {
                    do
                    {
                        node.Attributes.Add(new XmlAttributeInfo
                        {
                            Name = reader.Name,
                            LocalName = reader.LocalName,
                            Prefix = reader.Prefix,
                            NamespaceUri = reader.NamespaceURI,
                            Value = reader.Value
                        });
                    } while (reader.MoveToNextAttribute());
                    reader.MoveToElement();
                }
            }
            nodes.Add(node);
        }

        // 進行編譯
        var precompiled = CompileBatchTemplate(nodes);

        var emptyDict = new Dictionary<string, object?>();
        var localContext = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // 寫出頁首
        foreach (var segment in precompiled.HeaderSegments)
        {
            await segment.WriteToAsync(destXml, emptyDict, localContext, cancellationToken).ConfigureAwait(false);
        }

        // 針對每筆資料列寫出內容區段
        if (precompiled.BodySegments.Count > 0)
        {
            bool isFirst = true;

            var pageBreakParagraphNode = new XmlNodeInfo
            {
                NodeType = XmlNodeType.Element,
                LocalName = "p",
                Prefix = "text",
                NamespaceUri = "urn:oasis:names:tc:opendocument:xmlns:text:1.0",
                IsEmpty = true,
                Attributes = new List<XmlAttributeInfo>
                {
                    new XmlAttributeInfo { LocalName = "style-name", Prefix = "text", NamespaceUri = "urn:oasis:names:tc:opendocument:xmlns:text:1.0", Value = "OdfKitPageBreak" }
                }
            };

            // 將分頁符編譯為靜態區段
            var pbMs = new MemoryStream();
            SerializeNodeToStream(pageBreakParagraphNode, pbMs);
            byte[] pageBreakBytes = pbMs.ToArray();

            await foreach (var row in dataSequence.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!isFirst)
                {
                    await destXml.WriteAsync(pageBreakBytes, 0, pageBreakBytes.Length, cancellationToken).ConfigureAwait(false);
                }
                isFirst = false;

                var rowContext = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var segment in precompiled.BodySegments)
                {
                    await segment.WriteToAsync(destXml, row, rowContext, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        // 寫出頁尾
        foreach (var segment in precompiled.FooterSegments)
        {
            await segment.WriteToAsync(destXml, emptyDict, localContext, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void SerializeNodeToStream(XmlNodeInfo node, Stream stream)
    {
        if (node.NodeType == XmlNodeType.Element)
        {
            var sb = new StringBuilder();
            sb.Append('<');
            if (!string.IsNullOrEmpty(node.Prefix))
            {
                sb.Append(node.Prefix);
                sb.Append(':');
            }
            sb.Append(node.LocalName);

            // 如果是 office:document-content 或 office:document-styles 根節點，補齊常用 namespace 宣告
            bool isRoot = node.Prefix == "office" && (node.LocalName == "document-content" || node.LocalName == "document-styles");
            var declaredPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var attr in node.Attributes)
            {
                if (attr.Prefix == "xmlns")
                {
                    declaredPrefixes.Add(attr.LocalName);
                }
                else if (string.IsNullOrEmpty(attr.Prefix) && attr.LocalName == "xmlns")
                {
                    declaredPrefixes.Add(string.Empty);
                }
            }

            foreach (var attr in node.Attributes)
            {
                sb.Append(' ');
                if (!string.IsNullOrEmpty(attr.Prefix))
                {
                    sb.Append(attr.Prefix);
                    sb.Append(':');
                }
                sb.Append(attr.LocalName);
                sb.Append("=\"");
                sb.Append(EscapeXmlAttribute(attr.Value));
                sb.Append('"');
            }

            if (isRoot)
            {
                var commonNamespaces = new (string Prefix, string Uri)[]
                {
                    ("style", "urn:oasis:names:tc:opendocument:xmlns:style:1.0"),
                    ("fo", "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"),
                    ("svg", "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0"),
                    ("table", "urn:oasis:names:tc:opendocument:xmlns:table:1.0"),
                    ("draw", "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0"),
                    ("xlink", "http://www.w3.org/1999/xlink"),
                    ("number", "urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0"),
                    ("chart", "urn:oasis:names:tc:opendocument:xmlns:chart:1.0"),
                    ("calcext", "urn:org:documentfoundation:names:experimental:calc:xmlns:calcext:1.0"),
                    ("loext", "urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0"),
                    ("dc", "http://purl.org/dc/elements/1.1/"),
                    ("meta", "urn:oasis:names:tc:opendocument:xmlns:meta:1.0"),
                    ("config", "urn:oasis:names:tc:opendocument:xmlns:config:1.0"),
                    ("script", "urn:oasis:names:tc:opendocument:xmlns:script:1.0"),
                    ("anim", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0"),
                    ("smil", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0"),
                    ("form", "urn:oasis:names:tc:opendocument:xmlns:form:1.0"),
                    ("pkg", "http://docs.oasis-open.org/opendocument/meta/pkg#"),
                    ("of", "urn:oasis:names:tc:opendocument:xmlns:of:1.2"),
                    ("oooc", "http://openoffice.org/2004/calc"),
                    ("math", "http://www.w3.org/1998/Math/MathML"),
                    ("dr3d", "urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0"),
                    ("db", "urn:oasis:names:tc:opendocument:xmlns:database:1.0"),
                    ("report", "urn:oasis:names:tc:opendocument:xmlns:report:1.0")
                };

                foreach (var ns in commonNamespaces)
                {
                    if (!declaredPrefixes.Contains(ns.Prefix))
                    {
                        sb.Append(" xmlns:");
                        sb.Append(ns.Prefix);
                        sb.Append("=\"");
                        sb.Append(ns.Uri);
                        sb.Append('"');
                    }
                }
            }

            if (node.IsEmpty)
            {
                sb.Append("/>");
            }
            else
            {
                sb.Append('>');
            }
            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            stream.Write(bytes, 0, bytes.Length);
        }
        else if (node.NodeType == XmlNodeType.EndElement)
        {
            var sb = new StringBuilder();
            sb.Append("</");
            if (!string.IsNullOrEmpty(node.Prefix))
            {
                sb.Append(node.Prefix);
                sb.Append(':');
            }
            sb.Append(node.LocalName);
            sb.Append('>');
            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            stream.Write(bytes, 0, bytes.Length);
        }
        else if (node.NodeType == XmlNodeType.Text || node.NodeType == XmlNodeType.Whitespace || node.NodeType == XmlNodeType.SignificantWhitespace)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(node.Value);
            stream.Write(bytes, 0, bytes.Length);
        }
        else if (node.NodeType == XmlNodeType.Comment)
        {
            string text = "<!--" + node.Value + "-->";
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }
        else if (node.NodeType == XmlNodeType.ProcessingInstruction)
        {
            string text = "<?" + node.Name + " " + node.Value + "?>";
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }
    }

    private static void WriteEscapedTextToStream(string text, Stream stream)
    {
        if (string.IsNullOrEmpty(text))
            return;
        byte[] bytes = Encoding.UTF8.GetBytes(EscapeXmlText(text));
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string EscapeXmlText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;");
    }

    private static string EscapeXmlAttribute(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&apos;");
    }

    private static void WriteXmlEscapedUtf8(Stream stream, string text)
    {
        // 使用高效的 XML 逸出 UTF-8 寫入器
        if (string.IsNullOrEmpty(text))
            return;

        int maxByteCount = Encoding.UTF8.GetMaxByteCount(text.Length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
        try
        {
            int last = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<' || c == '>' || c == '&' || c == '"' || c == '\'')
                {
                    if (i > last)
                    {
                        int bytesWritten = Encoding.UTF8.GetBytes(text, last, i - last, buffer, 0);
                        stream.Write(buffer, 0, bytesWritten);
                    }
                    byte[] escape = c switch
                    {
                        '<' => "&lt;"u8.ToArray(),
                        '>' => "&gt;"u8.ToArray(),
                        '&' => "&amp;"u8.ToArray(),
                        '"' => "&quot;"u8.ToArray(),
                        _ => "&apos;"u8.ToArray()
                    };
                    stream.Write(escape, 0, escape.Length);
                    last = i + 1;
                }
            }
            if (last < text.Length)
            {
                int bytesWritten = Encoding.UTF8.GetBytes(text, last, text.Length - last, buffer, 0);
                stream.Write(buffer, 0, bytesWritten);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static object? GetValueWithPath(object data, string path, Dictionary<string, object?> localContext)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        string[] parts = path.Split('.');
        object? current = null;

        string root = parts[0];
        if (localContext.TryGetValue(root, out var localVal))
        {
            current = localVal;
        }
        else
        {
            current = MailMergeExpressionCache.GetValue(data, root);
        }

        for (int i = 1; i < parts.Length; i++)
        {
            if (current is null)
                return null;
            current = MailMergeExpressionCache.GetValue(current, parts[i]);
        }

        return current;
    }

}
