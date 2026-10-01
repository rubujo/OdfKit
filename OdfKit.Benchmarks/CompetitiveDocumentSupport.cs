using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OdfKit.Text;
using OpenXmlText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace OdfKit.Benchmarks;

/// <summary>
/// A single deterministic benchmark node of the document comparison: a heading (level 1 to 3) or a paragraph.
/// 文件對比的一個決定性基準節點：標題（層級 1 至 3）或段落。
/// </summary>
/// <param name="HeadingLevel">The heading level, or 0 for a paragraph. / 標題層級；段落為 0。</param>
/// <param name="Text">The node text. / 節點文字。</param>
internal readonly record struct CompetitiveDocumentNode(int HeadingLevel, string Text);

/// <summary>
/// A content checksum over every node of the document comparison dataset, used to prove each reader returned
/// exactly what the generator wrote, including node order.
/// 文件對比資料集的內容檢查碼，涵蓋每個節點（含順序），用來證明各讀取器讀回的內容與產生器寫入的完全一致。
/// </summary>
/// <param name="Paragraphs">The number of paragraphs. / 段落數。</param>
/// <param name="Headings">The number of headings. / 標題數。</param>
/// <param name="HeadingLevelSum">The sum of heading levels. / 標題層級總和。</param>
/// <param name="TextChars">The total text length. / 文字總長度。</param>
/// <param name="Hash">An order-sensitive FNV-1a hash of kind, level and text. / 對節點種類、層級與文字做的順序相依 FNV-1a 雜湊。</param>
internal readonly record struct CompetitiveDocumentChecksum(
    long Paragraphs,
    long Headings,
    long HeadingLevelSum,
    long TextChars,
    ulong Hash)
{
    /// <summary>
    /// Serializes the checksum to a single machine-readable line.
    /// 將檢查碼序列化為單行機器可讀文字。
    /// </summary>
    /// <returns>The serialized checksum. / 序列化後的檢查碼。</returns>
    internal string Serialize() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Paragraphs};{Headings};{HeadingLevelSum};{TextChars};{Hash}");

    /// <summary>
    /// Parses a checksum produced by <see cref="Serialize"/>.
    /// 解析由 <see cref="Serialize"/> 產生的檢查碼。
    /// </summary>
    /// <param name="text">The serialized checksum. / 序列化的檢查碼。</param>
    /// <returns>The parsed checksum. / 解析後的檢查碼。</returns>
    internal static CompetitiveDocumentChecksum Parse(string text)
    {
        string[] p = text.Split(';');
        return new CompetitiveDocumentChecksum(
            long.Parse(p[0], CultureInfo.InvariantCulture),
            long.Parse(p[1], CultureInfo.InvariantCulture),
            long.Parse(p[2], CultureInfo.InvariantCulture),
            long.Parse(p[3], CultureInfo.InvariantCulture),
            ulong.Parse(p[4], CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Accumulates a <see cref="CompetitiveDocumentChecksum"/> one node at a time.
/// 逐節點累加 <see cref="CompetitiveDocumentChecksum"/>。
/// </summary>
internal sealed class CompetitiveDocumentAccumulator
{
    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private long _paragraphs;
    private long _headings;
    private long _headingLevelSum;
    private long _textChars;
    private ulong _hash = FnvOffset;

    /// <summary>
    /// Adds one node.
    /// 加入一個節點。
    /// </summary>
    /// <param name="headingLevel">The heading level, or 0 for a paragraph. / 標題層級；段落為 0。</param>
    /// <param name="text">The node text. / 節點文字。</param>
    internal void Add(int headingLevel, string text)
    {
        if (headingLevel > 0)
        {
            _headings++;
            _headingLevelSum += headingLevel;
        }
        else
        {
            _paragraphs++;
        }

        _textChars += text.Length;
        Mix((ulong)headingLevel);
        foreach (char character in text)
        {
            Mix(character);
        }

        // 節點之間的分隔，使「AB」與「A」「B」的雜湊不同。
        Mix(0xFFFF);
    }

    /// <summary>
    /// Produces the checksum of everything added so far.
    /// 產生目前已加入內容的檢查碼。
    /// </summary>
    /// <returns>The checksum. / 檢查碼。</returns>
    internal CompetitiveDocumentChecksum ToChecksum() =>
        new(_paragraphs, _headings, _headingLevelSum, _textChars, _hash);

    private void Mix(ulong value)
    {
        _hash ^= value;
        _hash *= FnvPrime;
    }
}

/// <summary>
/// Deterministic node generator shared by the document comparison scenarios.
/// 供文件對比情境共用的決定性節點產生器。
/// </summary>
internal static class CompetitiveDocumentData
{
    /// <summary>
    /// The node count (500,000), which stays within the default limits of <see cref="OdtStreamReader"/>
    /// (1,000,000 nodes and 64 MiB of XML characters), so no safety limit has to be relaxed.
    /// 節點數（50 萬），落在 <see cref="OdtStreamReader"/> 的預設限制內（100 萬個節點與 64 MiB XML 字元），
    /// 因此不需要放寬任何安全限制。
    /// </summary>
    internal const int NodeCount = 500_000;

    /// <summary>
    /// A heading is generated every this many nodes.
    /// 每隔這麼多個節點產生一個標題。
    /// </summary>
    internal const int HeadingEvery = 50;

    private const int Seed = 20260709;

    private static readonly string[] s_words = ["Alpha", "Beta", "Gamma", "Delta", "Epsilon"];

    /// <summary>
    /// Generates <see cref="NodeCount"/> deterministic nodes in a lazily evaluated sequence.
    /// 以延遲求值的序列產生 <see cref="NodeCount"/> 個決定性節點。
    /// </summary>
    /// <returns>The nodes. / 節點序列。</returns>
    internal static IEnumerable<CompetitiveDocumentNode> Generate()
    {
        var random = new Random(Seed);
        for (int index = 0; index < NodeCount; index++)
        {
            if (index % HeadingEvery == 0)
            {
                int chapter = (index / HeadingEvery) + 1;
                yield return new CompetitiveDocumentNode(
                    (index / HeadingEvery % 3) + 1,
                    string.Create(CultureInfo.InvariantCulture, $"第 {chapter} 章 {s_words[chapter % s_words.Length]}"));
            }
            else
            {
                yield return new CompetitiveDocumentNode(
                    0,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"第 {index:D7} 段：OdfKit 效能對比 {s_words[index % s_words.Length]}，隨機值 {random.Next(1000, 9999)}"));
            }
        }
    }

    /// <summary>
    /// Computes the expected checksum directly from the generator.
    /// 直接由產生器計算預期的檢查碼。
    /// </summary>
    /// <returns>The expected checksum. / 預期的檢查碼。</returns>
    internal static CompetitiveDocumentChecksum ComputeExpected()
    {
        var accumulator = new CompetitiveDocumentAccumulator();
        foreach (CompetitiveDocumentNode node in Generate())
        {
            accumulator.Add(node.HeadingLevel, node.Text);
        }

        return accumulator.ToChecksum();
    }
}

/// <summary>
/// Write and read implementations of the ODT-versus-DOCX streaming comparison. This is a cross-format
/// reference comparison: OdfKit streams ODF text documents (.odt) while the Open XML SDK streams
/// WordprocessingML documents (.docx) through <see cref="OpenXmlWriter"/> and <see cref="OpenXmlReader"/>.
/// ODT 對 DOCX 串流對比的寫入與讀取實作。這是跨格式參考對比：OdfKit 串流處理 ODF 文字文件（.odt），
/// Open XML SDK 則以 <see cref="OpenXmlWriter"/> 與 <see cref="OpenXmlReader"/> 串流處理 WordprocessingML 文件（.docx）。
/// </summary>
internal static class CompetitiveDocumentStreams
{
    /// <summary>
    /// Writes the dataset with <see cref="OdtStreamWriter"/>.
    /// 以 <see cref="OdtStreamWriter"/> 寫入資料集。
    /// </summary>
    /// <param name="output">The destination stream. / 目標資料流。</param>
    internal static void WriteOdt(Stream output)
    {
        using var writer = new OdtStreamWriter(output);
        foreach (CompetitiveDocumentNode node in CompetitiveDocumentData.Generate())
        {
            if (node.HeadingLevel > 0)
            {
                writer.AddHeading(node.Text, node.HeadingLevel);
            }
            else
            {
                writer.AddParagraph(node.Text);
            }
        }
    }

    /// <summary>
    /// Writes the dataset with the Open XML SDK's streaming <see cref="OpenXmlWriter"/>.
    /// 以 Open XML SDK 的串流式 <see cref="OpenXmlWriter"/> 寫入資料集。
    /// </summary>
    /// <param name="output">The destination stream. / 目標資料流。</param>
    internal static void WriteDocx(Stream output)
    {
        using var document = WordprocessingDocument.Create(output, WordprocessingDocumentType.Document);
        MainDocumentPart mainPart = document.AddMainDocumentPart();
        using (OpenXmlWriter writer = OpenXmlWriter.Create(mainPart))
        {
            writer.WriteStartElement(new Document());
            writer.WriteStartElement(new Body());
            foreach (CompetitiveDocumentNode node in CompetitiveDocumentData.Generate())
            {
                writer.WriteStartElement(new Paragraph());
                if (node.HeadingLevel > 0)
                {
                    writer.WriteElement(new ParagraphProperties(
                        new ParagraphStyleId
                        {
                            Val = string.Create(CultureInfo.InvariantCulture, $"Heading{node.HeadingLevel}"),
                        }));
                }

                writer.WriteElement(new Run(new OpenXmlText(node.Text) { Space = SpaceProcessingModeValues.Preserve }));
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
        }
    }

    /// <summary>
    /// Reads the .odt dataset with <see cref="OdtStreamReader"/>.
    /// 以 <see cref="OdtStreamReader"/> 讀取 .odt 資料集。
    /// </summary>
    /// <param name="path">The .odt file path. / .odt 檔案路徑。</param>
    /// <returns>The checksum of the nodes read. / 讀到節點的檢查碼。</returns>
    internal static CompetitiveDocumentChecksum ReadOdt(string path)
    {
        var accumulator = new CompetitiveDocumentAccumulator();
        using var reader = new OdtStreamReader(path);
        while (reader.Read())
        {
            accumulator.Add(
                reader.NodeType == OdtNodeType.Heading ? reader.HeadingLevel : 0,
                reader.Text);
        }

        return accumulator.ToChecksum();
    }

    /// <summary>
    /// Reads the .docx dataset with the Open XML SDK's streaming <see cref="OpenXmlReader"/>.
    /// 以 Open XML SDK 的串流式 <see cref="OpenXmlReader"/> 讀取 .docx 資料集。
    /// </summary>
    /// <param name="path">The .docx file path. / .docx 檔案路徑。</param>
    /// <returns>The checksum of the nodes read. / 讀到節點的檢查碼。</returns>
    internal static CompetitiveDocumentChecksum ReadDocx(string path)
    {
        var accumulator = new CompetitiveDocumentAccumulator();
        var text = new StringBuilder();
        int level = 0;
        using var document = WordprocessingDocument.Open(path, isEditable: false);
        MainDocumentPart mainPart = document.MainDocumentPart
            ?? throw new InvalidDataException("DOCX 缺少主文件部分。");
        using OpenXmlReader reader = OpenXmlReader.Create(mainPart);
        while (reader.Read())
        {
            Type type = reader.ElementType;
            if (type == typeof(Paragraph))
            {
                if (reader.IsStartElement)
                {
                    text.Clear();
                    level = 0;
                }
                else if (reader.IsEndElement)
                {
                    accumulator.Add(level, text.ToString());
                }
            }
            else if (type == typeof(ParagraphStyleId) && reader.IsStartElement)
            {
                foreach (OpenXmlAttribute attribute in reader.Attributes)
                {
                    string? value = attribute.Value;
                    if (attribute.LocalName == "val" &&
                        value is not null &&
                        value.StartsWith("Heading", StringComparison.Ordinal))
                    {
                        level = int.Parse(value.AsSpan(7), CultureInfo.InvariantCulture);
                    }
                }
            }
            else if (type == typeof(OpenXmlText) && reader.IsStartElement)
            {
                text.Append(reader.GetText());
            }
        }

        return accumulator.ToChecksum();
    }
}
