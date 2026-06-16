using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using OdfKit.Core;

namespace OdfKit.Text;

/// <summary>
/// 以低記憶體流式方式逐段落讀取 ODT 文字文件，適用於大型文件文字擷取。
/// </summary>
public sealed class OdtStreamReader : IDisposable
{
    private readonly ZipArchive _zip;
    private Stream? _contentStream;
    private XmlReader? _reader;
    private bool _started;

    /// <summary>
    /// 從資料流初始化 <see cref="OdtStreamReader"/> 類別的新執行個體。
    /// </summary>
    /// <param name="stream">ODT 檔案資料流。</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="stream"/> 為 null 時擲出。</exception>
    public OdtStreamReader(Stream stream)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        _zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
    }

    /// <summary>
    /// 從檔案路徑初始化 <see cref="OdtStreamReader"/> 類別的新執行個體。
    /// </summary>
    /// <param name="path">ODT 檔案路徑。</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="path"/> 為 null 時擲出。</exception>
    public OdtStreamReader(string path)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        _zip = ZipFile.OpenRead(path);
    }

    /// <summary>
    /// 取得目前元素的類型。
    /// </summary>
    public OdtNodeType NodeType { get; private set; } = OdtNodeType.Other;

    /// <summary>
    /// 取得目前元素的純文字內容，包含內嵌 <c>text:span</c> 文字。
    /// </summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>
    /// 取得目前元素的段落樣式名稱。
    /// </summary>
    public string? StyleName { get; private set; }

    /// <summary>
    /// 取得標題層級，僅在 <see cref="NodeType"/> 為 <see cref="OdtNodeType.Heading"/> 時有效。
    /// </summary>
    public int HeadingLevel { get; private set; }

    /// <summary>
    /// 讀取下一個文字元素；回傳 false 代表文件結束。
    /// </summary>
    /// <returns>若成功讀取元素則為 true，否則為 false。</returns>
    public bool Read()
    {
        if (!_started)
        {
            OpenContentReader();
            _started = true;
        }

        if (_reader is null)
        {
            return false;
        }

        while (_reader.Read())
        {
            if (_reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (_reader.NamespaceURI == OdfNamespaces.Text)
            {
                if (_reader.LocalName == "p")
                {
                    CaptureCurrentElement(OdtNodeType.Paragraph, headingLevel: 0);
                    return true;
                }

                if (_reader.LocalName == "h")
                {
                    int headingLevel = ParseHeadingLevel(_reader.GetAttribute("outline-level", OdfNamespaces.Text));
                    CaptureCurrentElement(OdtNodeType.Heading, headingLevel);
                    return true;
                }

                if (_reader.LocalName == "list-item")
                {
                    CaptureCurrentElement(OdtNodeType.ListItem, headingLevel: 0);
                    return true;
                }
            }

            if (_reader.NamespaceURI == OdfNamespaces.Table && _reader.LocalName == "table-cell")
            {
                CaptureCurrentElement(OdtNodeType.TableCell, headingLevel: 0);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 釋放讀取器與底層 ZIP 資源。
    /// </summary>
    public void Dispose()
    {
        _reader?.Dispose();
        _contentStream?.Dispose();
        _zip.Dispose();
    }

    private void OpenContentReader()
    {
        var entry = _zip.GetEntry("content.xml")
            ?? throw new InvalidOperationException("ODT 檔案缺少 content.xml 項目。");
        _contentStream = entry.Open();
        _reader = XmlReader.Create(_contentStream, CreateXmlReaderSettings());
    }

    private void CaptureCurrentElement(OdtNodeType nodeType, int headingLevel)
    {
        NodeType = nodeType;
        HeadingLevel = headingLevel;
        StyleName = _reader!.GetAttribute("style-name", OdfNamespaces.Text);
        Text = ReadCurrentElementText(_reader);
    }

    private static string ReadCurrentElementText(XmlReader reader)
    {
        if (reader.IsEmptyElement)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        using var subtree = reader.ReadSubtree();
        subtree.Read();
        while (subtree.Read())
        {
            if (subtree.NodeType is XmlNodeType.Text or XmlNodeType.CDATA)
            {
                builder.Append(subtree.Value);
            }
            else if (subtree.NodeType == XmlNodeType.Element && subtree.NamespaceURI == OdfNamespaces.Text)
            {
                AppendTextControl(builder, subtree);
            }
        }

        return builder.ToString();
    }

    private static void AppendTextControl(StringBuilder builder, XmlReader reader)
    {
        if (reader.LocalName == "s")
        {
            int count = ParsePositiveInt(reader.GetAttribute("c", OdfNamespaces.Text), defaultValue: 1);
            builder.Append(' ', count);
        }
        else if (reader.LocalName == "tab")
        {
            builder.Append('\t');
        }
        else if (reader.LocalName == "line-break")
        {
            builder.Append('\n');
        }
    }

    private static int ParseHeadingLevel(string? value)
    {
        int level = ParsePositiveInt(value, defaultValue: 1);
        return Math.Min(level, 6);
    }

    private static int ParsePositiveInt(string? value, int defaultValue)
    {
        return int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int parsed) && parsed > 0
            ? parsed
            : defaultValue;
    }

    private static XmlReaderSettings CreateXmlReaderSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null
    };
}
