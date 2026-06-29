using System;
using System.Globalization;
using System.Xml;
using OdfKit.Core;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Writes a single ODS worksheet XML fragment.
/// 提供單一 ODS 工作表 XML 片段的寫入 API。
/// </summary>
public sealed class OdsSheetWriter
{
    private readonly XmlWriter _writer;
    private bool _isRowStarted;

    internal OdsSheetWriter(XmlWriter writer) => _writer = writer ?? throw new ArgumentNullException(nameof(writer));

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

        _writer.WriteStartElement("table", "table-row", OdfNamespaces.Table);
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

        _writer.WriteEndElement();
        _isRowStarted = false;
    }

    /// <summary>
    /// Writes a string cell.
    /// 寫入字串型態的儲存格。
    /// </summary>
    /// <param name="value">The cell text. / 儲存格文字。</param>
    public void WriteCell(string value)
    {
        WriteCellStart("string");
        _writer.WriteStartElement("text", "p", OdfNamespaces.Text);
        if (!string.IsNullOrEmpty(value))
        {
            _writer.WriteString(value);
        }

        _writer.WriteEndElement();
        _writer.WriteEndElement();
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
        _writer.WriteAttributeString("office", "value", OdfNamespaces.Office, text);
        _writer.WriteStartElement("text", "p", OdfNamespaces.Text);
        _writer.WriteString(text);
        _writer.WriteEndElement();
        _writer.WriteEndElement();
    }

    /// <summary>
    /// Writes a Boolean cell.
    /// 寫入布林型態的儲存格。
    /// </summary>
    /// <param name="value">The cell Boolean value. / 儲存格布林值。</param>
    public void WriteCell(bool value)
    {
        WriteCellStart("boolean");
        _writer.WriteAttributeString("office", "boolean-value", OdfNamespaces.Office, value ? "true" : "false");
        _writer.WriteStartElement("text", "p", OdfNamespaces.Text);
        _writer.WriteString(value ? "TRUE" : "FALSE");
        _writer.WriteEndElement();
        _writer.WriteEndElement();
    }

    internal void CloseOpenRow() => WriteEndRow();

    private void WriteCellStart(string valueType)
    {
        if (!_isRowStarted)
        {
            WriteStartRow();
        }

        _writer.WriteStartElement("table", "table-cell", OdfNamespaces.Table);
        _writer.WriteAttributeString("office", "value-type", OdfNamespaces.Office, valueType);
    }
}
