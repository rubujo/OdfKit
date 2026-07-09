using System;
using System.Globalization;
using OdfKit.Core;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Writes a single ODS worksheet XML fragment.
/// 提供單一 ODS 工作表 XML 片段的寫入 API。
/// </summary>
public sealed class OdsSheetWriter
{
    private readonly OdfRawXmlWriter _writer;
    private bool _isRowStarted;

    internal OdsSheetWriter(OdfRawXmlWriter writer) => _writer = writer ?? throw new ArgumentNullException(nameof(writer));

    /// <summary>
    /// Starts writing a new data row.
    /// 開始寫入一個新的資料列。
    /// </summary>
    public void WriteStartRow()
    {
        if (_isRowStarted)
        {
            WriteEndRow();
        }

        _writer.WriteStartElement("table:table-row");
        _isRowStarted = true;
    }

    /// <summary>
    /// Ends writing the current data row.
    /// 結束目前資料列的寫入。
    /// </summary>
    public void WriteEndRow()
    {
        if (!_isRowStarted)
        {
            return;
        }

        _writer.WriteEndElement("table:table-row");
        _isRowStarted = false;
    }

    /// <summary>
    /// Writes a string cell.
    /// 寫入字串型態的儲存格。
    /// </summary>
    /// <param name="value">The cell text. / 儲存格文字。</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> contains a character that is not valid in XML 1.0. / 當 <paramref name="value"/> 含有 XML 1.0 不允許的字元時擲出。</exception>
    public void WriteCell(string value)
    {
        // null 與空字串語意維持既有行為：寫出空的 text:p，不擲出例外。
        ReadOnlySpan<char> text = value is null ? default : value.AsSpan();
        // 目標寫入器已關閉 CheckCharacters，寫入前先以輕量防線驗證使用者文字。
        OdfXmlCharacterGuard.ValidateText(text, nameof(value));
        WriteCellStart("string");
        _writer.WriteStartElement("text:p");
        if (!text.IsEmpty)
        {
            _writer.WriteText(text);
        }

        _writer.WriteEndElement("text:p");
        _writer.WriteEndElement("table:table-cell");
    }

    /// <summary>
    /// Writes a numeric cell.
    /// 寫入數值型態的儲存格。
    /// </summary>
    /// <param name="value">The cell numeric value. / 儲存格數值。</param>
    public void WriteCell(double value)
    {
        string text = value.ToString(CultureInfo.InvariantCulture);
        WriteCellStart("float");
        _writer.WriteAttribute("office:value", text.AsSpan());
        _writer.WriteStartElement("text:p");
        _writer.WriteText(text.AsSpan());
        _writer.WriteEndElement("text:p");
        _writer.WriteEndElement("table:table-cell");
    }

    /// <summary>
    /// Writes a Boolean cell.
    /// 寫入布林型態的儲存格。
    /// </summary>
    /// <param name="value">The cell Boolean value. / 儲存格布林值。</param>
    public void WriteCell(bool value)
    {
        WriteCellStart("boolean");
        _writer.WriteAttribute("office:boolean-value", value ? "true".AsSpan() : "false".AsSpan());
        _writer.WriteStartElement("text:p");
        _writer.WriteText(value ? "TRUE".AsSpan() : "FALSE".AsSpan());
        _writer.WriteEndElement("text:p");
        _writer.WriteEndElement("table:table-cell");
    }

    internal void CloseOpenRow() => WriteEndRow();

    private void WriteCellStart(string valueType)
    {
        if (!_isRowStarted)
        {
            WriteStartRow();
        }

        _writer.WriteStartElement("table:table-cell");
        _writer.WriteAttribute("office:value-type", valueType.AsSpan());
    }
}
