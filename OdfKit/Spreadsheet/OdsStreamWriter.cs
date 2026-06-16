using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 提供以資料流方式寫入 ODS 試算表文件的功能，以支援高效能、低記憶體耗用的寫入作業。
/// </summary>
public class OdsStreamWriter : IDisposable
{
    private readonly Stream _outputStream;
    private readonly ZipArchive _zip;
    private readonly Stream _contentEntryStream;
    private readonly XmlWriter _writer;
    private bool _isRowStarted;
    private bool _isSheetStarted;
    private bool _disposed;
    private readonly System.Collections.Generic.List<(string styleName, OdfLength width)> _columnStyles = [];
    private readonly System.Collections.Generic.List<(string styleName, OdfLength? height, bool useOptimalHeight)> _rowStyles = [];
    private int _autoColumnStyleIndex = 0;
    private int _autoRowStyleIndex = 0;
    private OdfVersion _version = OdfVersionInfo.DefaultVersion;

    /// <summary>
    /// 取得或設定寫入之 ODS 文件的 ODF 版本。
    /// </summary>
    public OdfVersion Version
    {
        get => _version;
        set => _version = value;
    }

    private static string FormatVersion(OdfVersion version)
    {
        return version switch
        {
            OdfVersion.Odf10 => "1.0",
            OdfVersion.Odf11 => "1.1",
            OdfVersion.Odf12 => "1.2",
            OdfVersion.Odf13 => "1.3",
            OdfVersion.Odf14 => "1.4",
            _ => "1.4"
        };
    }

    /// <summary>
    /// 初始化 <see cref="OdsStreamWriter"/> 類別的新執行個體。
    /// </summary>
    /// <param name="outputStream">用來輸出 ODS 文件的目標資料流</param>
    /// <param name="version">要寫入的 ODF 規格版本</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="outputStream"/> 為 null 時擲出</exception>
    public OdsStreamWriter(Stream outputStream, OdfVersion version = OdfVersion.Odf14)
    {
        _outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
        _version = version;

        // 包裝於 NonSeekableStreamWrapper 以強制 ZipArchive 使用資料流、非緩衝模式
        _zip = new ZipArchive(new NonSeekableStreamWrapper(_outputStream), ZipArchiveMode.Create, leaveOpen: true);

        // 1. 先寫入未壓縮的 mimetype
        var mimeEntry = _zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var s = mimeEntry.Open())
        {
            byte[] bytes = Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.spreadsheet");
            s.Write(bytes, 0, bytes.Length);
        }

        // 2. 寫入預設的中階資料、樣式與資訊清單項目
        WriteDefaultMetaFiles();

        // 3. 開啟 content.xml 以進行資料流寫入
        var contentEntry = _zip.CreateEntry("content.xml", CompressionLevel.Fastest);
        _contentEntryStream = contentEntry.Open();

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false // 最小化大小
        };
        _writer = XmlWriter.Create(_contentEntryStream, settings);

        // 寫入 ODF XML 標頭與根 document-content 標籤
        _writer.WriteStartDocument();
        _writer.WriteStartElement("office", "document-content", OdfNamespaces.Office);
        _writer.WriteAttributeString("office", "version", OdfNamespaces.Office, FormatVersion(_version));
        _writer.WriteAttributeString("xmlns", "office", null, OdfNamespaces.Office);
        _writer.WriteAttributeString("xmlns", "table", null, OdfNamespaces.Table);
        _writer.WriteAttributeString("xmlns", "text", null, OdfNamespaces.Text);
        _writer.WriteAttributeString("xmlns", "style", null, OdfNamespaces.Style);

        // 寫入 body 與 spreadsheet 包裝器
        _writer.WriteStartElement("office", "body", OdfNamespaces.Office);
        _writer.WriteStartElement("office", "spreadsheet", OdfNamespaces.Office);
    }

    /// <summary>
    /// 開始寫入一個新的工作表。
    /// </summary>
    /// <param name="sheetName">工作表名稱</param>
    public void WriteStartSheet(string sheetName)
    {
        if (_disposed)
            return;
        if (_isSheetStarted)
            WriteEndSheet();
        _writer.WriteStartElement("table", "table", OdfNamespaces.Table);
        _writer.WriteAttributeString("table", "name", OdfNamespaces.Table, sheetName);
        _isSheetStarted = true;
    }

    /// <summary>
    /// 寫入資料欄定義。
    /// </summary>
    /// <param name="width">資料欄寬度</param>
    /// <param name="styleName">樣式名稱，如果為 null 則自動產生</param>
    public void WriteColumn(OdfLength width, string? styleName = null)
    {
        if (_disposed)
            return;
        string name = string.IsNullOrEmpty(styleName)
            ? $"co_auto_{++_autoColumnStyleIndex}"
            : styleName!;

        _writer.WriteStartElement("table", "table-column", OdfNamespaces.Table);
        _writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, name);
        _writer.WriteEndElement();

        _columnStyles.Add((name, width));
    }

    /// <summary>
    /// 開始寫入一個新的資料列。
    /// </summary>
    /// <param name="height">資料列高度</param>
    /// <param name="styleName">樣式名稱</param>
    /// <param name="useOptimalHeight">是否使用最佳高度</param>
    public void WriteStartRow(double? height = null, string? styleName = null, bool useOptimalHeight = false)
    {
        if (_disposed)
            return;
        if (_isRowStarted)
            WriteEndRow();
        _isRowStarted = true;

        string? resolvedStyleName = styleName;
        if (height.HasValue || useOptimalHeight)
        {
            resolvedStyleName = string.IsNullOrEmpty(styleName)
                ? $"ro_auto_{++_autoRowStyleIndex}"
                : styleName;
            OdfLength? rowHeight = height.HasValue
                ? OdfLength.FromPoints(height.Value)
                : (OdfLength?)null;
            _rowStyles.Add((resolvedStyleName!, rowHeight, useOptimalHeight));
        }

        _writer.WriteStartElement("table", "table-row", OdfNamespaces.Table);
        if (!string.IsNullOrEmpty(resolvedStyleName))
        {
            _writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, resolvedStyleName);
        }
    }

    /// <summary>
    /// 寫入字串型態的儲存格。
    /// </summary>
    /// <param name="value">儲存格的值</param>
    /// <param name="styleName">樣式名稱</param>
    public void WriteCell(string value, string? styleName = null)
    {
        if (_disposed)
            return;
        _writer.WriteStartElement("table", "table-cell", OdfNamespaces.Table);
        _writer.WriteAttributeString("office", "value-type", OdfNamespaces.Office, "string");
        if (!string.IsNullOrEmpty(styleName))
        {
            _writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, styleName);
        }
        _writer.WriteStartElement("text", "p", OdfNamespaces.Text);
        _writer.WriteString(value);
        _writer.WriteEndElement(); // text:p
        _writer.WriteEndElement(); // table:cell
    }

    /// <summary>
    /// 寫入數值型態的儲存格。
    /// </summary>
    /// <param name="value">儲存格的數值</param>
    /// <param name="styleName">樣式名稱</param>
    public void WriteCell(double value, string? styleName = null)
    {
        if (_disposed)
            return;
        _writer.WriteStartElement("table", "table-cell", OdfNamespaces.Table);
        _writer.WriteAttributeString("office", "value-type", OdfNamespaces.Office, "float");
        _writer.WriteAttributeString("office", "value", OdfNamespaces.Office, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(styleName))
        {
            _writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, styleName);
        }
        _writer.WriteStartElement("text", "p", OdfNamespaces.Text);
        _writer.WriteString(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _writer.WriteEndElement(); // text:p
        _writer.WriteEndElement(); // table:cell
    }

    /// <summary>
    /// 寫入日期時間型態的儲存格。
    /// </summary>
    /// <param name="value">儲存格的日期時間值</param>
    /// <param name="styleName">樣式名稱</param>
    /// <param name="timezoneNaive">是否忽略時區轉換</param>
    public void WriteCell(DateTime value, string? styleName = null, bool timezoneNaive = false)
    {
        if (_disposed)
            return;
        _writer.WriteStartElement("table", "table-cell", OdfNamespaces.Table);
        _writer.WriteAttributeString("office", "value-type", OdfNamespaces.Office, "date");

        string isoDate;
        if (value == DateTime.MinValue || value == DateTime.MaxValue)
        {
            isoDate = timezoneNaive
                ? value.ToString("s", System.Globalization.CultureInfo.InvariantCulture)
                : value.ToString("s", System.Globalization.CultureInfo.InvariantCulture) + "Z";
        }
        else
        {
            isoDate = timezoneNaive
                ? value.ToString("s", System.Globalization.CultureInfo.InvariantCulture)
                : value.ToUniversalTime().ToString("s", System.Globalization.CultureInfo.InvariantCulture) + "Z";
        }

        _writer.WriteAttributeString("office", "date-value", OdfNamespaces.Office, isoDate);
        if (!string.IsNullOrEmpty(styleName))
        {
            _writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, styleName);
        }
        _writer.WriteStartElement("text", "p", OdfNamespaces.Text);
        _writer.WriteString(isoDate);
        _writer.WriteEndElement(); // text:p
        _writer.WriteEndElement(); // table:cell
    }

    /// <summary>
    /// 寫入布林值型態的儲存格。
    /// </summary>
    /// <param name="value">儲存格的布林值</param>
    /// <param name="styleName">樣式名稱</param>
    public void WriteCell(bool value, string? styleName = null)
    {
        if (_disposed)
            return;
        _writer.WriteStartElement("table", "table-cell", OdfNamespaces.Table);
        _writer.WriteAttributeString("office", "value-type", OdfNamespaces.Office, "boolean");
        _writer.WriteAttributeString("office", "boolean-value", OdfNamespaces.Office, value ? "true" : "false");
        if (!string.IsNullOrEmpty(styleName))
        {
            _writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, styleName);
        }
        _writer.WriteStartElement("text", "p", OdfNamespaces.Text);
        _writer.WriteString(value ? "TRUE" : "FALSE");
        _writer.WriteEndElement(); // text:p
        _writer.WriteEndElement(); // table:cell
    }

    /// <summary>
    /// 結束目前資料列的寫入。
    /// </summary>
    public void WriteEndRow()
    {
        if (_disposed)
            return;
        if (_isRowStarted)
        {
            _writer.WriteEndElement(); // table-row
            _isRowStarted = false;
        }
    }

    /// <summary>
    /// 結束目前工作表的寫入。
    /// </summary>
    public void WriteEndSheet()
    {
        if (_disposed)
            return;
        if (_isRowStarted)
            WriteEndRow();
        if (_isSheetStarted)
        {
            _writer.WriteEndElement(); // table:table
            _isSheetStarted = false;
        }
    }

    private void WriteDefaultMetaFiles()
    {
        WriteManifest();
        // 此處不寫入 styles.xml
        WriteMeta();
    }

    private void WriteManifest()
    {
        var entry = _zip.CreateEntry("META-INF/manifest.xml", CompressionLevel.Optimal);
        using (var stream = entry.Open())
        using (var writer = XmlWriter.Create(stream))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("manifest", "manifest", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "version", OdfNamespaces.Manifest, FormatVersion(_version));

            writer.WriteStartElement("file-entry", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, "/");
            writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, "application/vnd.oasis.opendocument.spreadsheet");
            writer.WriteEndElement();

            writer.WriteStartElement("file-entry", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, "content.xml");
            writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, "text/xml");
            writer.WriteEndElement();

            writer.WriteStartElement("file-entry", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, "styles.xml");
            writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, "text/xml");
            writer.WriteEndElement();

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }
    }

    private void WriteStyles()
    {
        var entry = _zip.CreateEntry("styles.xml", CompressionLevel.Optimal);
        using (var stream = entry.Open())
        using (var writer = XmlWriter.Create(stream))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("office", "document-styles", OdfNamespaces.Office);
            writer.WriteAttributeString("office", "version", OdfNamespaces.Office, FormatVersion(_version));
            writer.WriteAttributeString("xmlns", "office", null, OdfNamespaces.Office);
            writer.WriteAttributeString("xmlns", "style", null, OdfNamespaces.Style);
            writer.WriteAttributeString("xmlns", "text", null, OdfNamespaces.Text);
            writer.WriteAttributeString("xmlns", "table", null, OdfNamespaces.Table);
            writer.WriteAttributeString("xmlns", "fo", null, OdfNamespaces.Fo);

            writer.WriteStartElement("office", "styles", OdfNamespaces.Office);
            writer.WriteEndElement();

            writer.WriteStartElement("office", "automatic-styles", OdfNamespaces.Office);
            foreach (var style in _columnStyles)
            {
                writer.WriteStartElement("style", "style", OdfNamespaces.Style);
                writer.WriteAttributeString("style", "name", OdfNamespaces.Style, style.styleName);
                writer.WriteAttributeString("style", "family", OdfNamespaces.Style, "table-column");
                writer.WriteStartElement("style", "table-column-properties", OdfNamespaces.Style);
                writer.WriteAttributeString("style", "column-width", OdfNamespaces.Style, style.width.ToString());
                writer.WriteEndElement(); // table-column-properties
                writer.WriteEndElement(); // style
            }

            foreach (var style in _rowStyles)
            {
                writer.WriteStartElement("style", "style", OdfNamespaces.Style);
                writer.WriteAttributeString("style", "name", OdfNamespaces.Style, style.styleName);
                writer.WriteAttributeString("style", "family", OdfNamespaces.Style, "table-row");
                writer.WriteStartElement("style", "table-row-properties", OdfNamespaces.Style);
                if (style.useOptimalHeight)
                {
                    writer.WriteAttributeString("style", "use-optimal-row-height", OdfNamespaces.Style, "true");
                }
                else if (style.height.HasValue)
                {
                    string heightCm = style.height.Value.ToCentimeters()
                        .ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "cm";
                    writer.WriteAttributeString("style", "row-height", OdfNamespaces.Style, heightCm);
                }
                writer.WriteEndElement(); // table-row-properties
                writer.WriteEndElement(); // style
            }
            writer.WriteEndElement();

            writer.WriteStartElement("office", "master-styles", OdfNamespaces.Office);
            writer.WriteEndElement();

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }
    }

    private void WriteMeta()
    {
        var entry = _zip.CreateEntry("meta.xml", CompressionLevel.Optimal);
        using (var stream = entry.Open())
        using (var writer = XmlWriter.Create(stream))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("office", "document-meta", OdfNamespaces.Office);
            writer.WriteAttributeString("office", "version", OdfNamespaces.Office, FormatVersion(_version));
            writer.WriteAttributeString("xmlns", "office", null, OdfNamespaces.Office);
            writer.WriteAttributeString("xmlns", "dc", null, OdfNamespaces.Dc);
            writer.WriteAttributeString("xmlns", "meta", null, OdfNamespaces.Meta);

            writer.WriteStartElement("office", "meta", OdfNamespaces.Office);
            writer.WriteEndElement();

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }
    }

    /// <summary>
    /// 關閉所有底層資料流並釋放 <see cref="OdsStreamWriter"/> 使用的資源。
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 釋放 <see cref="OdsStreamWriter"/> 類別所使用的非受控資源，並選擇性釋放受控資源。
    /// </summary>
    /// <param name="disposing">為 true 則釋放受控與非受控資源；為 false 則僅釋放非受控資源</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        _disposed = true;

        if (disposing)
        {
            if (_isSheetStarted)
                WriteEndSheet();

            // 關閉 spreadsheet、body、document-content 標籤
            _writer.WriteEndElement(); // office:spreadsheet
            _writer.WriteEndElement(); // office:body
            _writer.WriteEndElement(); // office:document-content
            _writer.WriteEndDocument();

            try
            { _writer.Dispose(); }
            catch { /* 盡量排清 XmlWriter */ }
            try
            { _contentEntryStream.Dispose(); }
            catch { }
            try
            { WriteStyles(); }
            catch { }
            _zip.Dispose();
        }
    }
}

internal class NonSeekableStreamWrapper(Stream baseStream) : Stream
{
    private readonly Stream _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));

    public override bool CanRead => _baseStream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => _baseStream.CanWrite;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _baseStream.Flush();

    public override int Read(byte[] buffer, int offset, int count) => _baseStream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => _baseStream.Write(buffer, offset, count);

    public override System.Threading.Tasks.Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
    {
        return _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
    {
        return _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override System.Threading.Tasks.Task FlushAsync(System.Threading.CancellationToken cancellationToken)
    {
        return _baseStream.FlushAsync(cancellationToken);
    }
}
