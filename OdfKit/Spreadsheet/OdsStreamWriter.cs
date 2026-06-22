using System.Collections.Generic;
using System.Globalization;
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
public partial class OdsStreamWriter : IDisposable
{
    #region Stream Writing
    private readonly Stream _outputStream;
    private readonly ZipArchive _zip;
    private readonly Stream _contentEntryStream;
    private readonly XmlWriter _writer;
    private bool _isRowStarted;
    private bool _isSheetStarted;
    private bool _disposed;
    private readonly List<(string styleName, OdfLength width)> _columnStyles = [];
    private readonly List<(string styleName, OdfLength? height, bool useOptimalHeight)> _rowStyles = [];
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

        // 若底層資料流支援尋覽 (CanSeek)，則直接使用以避免 ZipArchive 強制寫入 Data Descriptor
        // 這能確保 mimetype 檔案不含 Data Descriptor，符合 ODF 封裝規格以防止 LibreOffice 報錯毀損
        Stream targetStream = _outputStream.CanSeek ? _outputStream : new NonSeekableStreamWrapper(_outputStream);
        _zip = new ZipArchive(targetStream, ZipArchiveMode.Create, leaveOpen: true);

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
        _writer.WriteAttributeString("office", "value", OdfNamespaces.Office, value.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(styleName))
        {
            _writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, styleName);
        }
        _writer.WriteStartElement("text", "p", OdfNamespaces.Text);
        _writer.WriteString(value.ToString(CultureInfo.InvariantCulture));
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
                ? value.ToString("s", CultureInfo.InvariantCulture)
                : value.ToString("s", CultureInfo.InvariantCulture) + "Z";
        }
        else
        {
            isoDate = timezoneNaive
                ? value.ToString("s", CultureInfo.InvariantCulture)
                : value.ToUniversalTime().ToString("s", CultureInfo.InvariantCulture) + "Z";
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
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"OdsStreamWriter 釋放 XmlWriter 時發生次要錯誤：{ex.Message}", ex);
            }

            try
            { _contentEntryStream.Dispose(); }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"OdsStreamWriter 釋放 content 串流時發生次要錯誤：{ex.Message}", ex);
            }

            try
            { WriteStyles(); }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"OdsStreamWriter 於 Dispose 寫入 styles.xml 失敗：{ex.Message}", ex);
            }
            _zip.Dispose();
        }
    }

    #endregion
}
