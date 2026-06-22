using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;

namespace OdfKit.Styles;

/// <summary>
/// 實作試算表儲存格的樣式代理 Facade，自動處理 Local Style 與屬性對照。
/// </summary>
public sealed class OdfCellStyleProxy
{
    private readonly OdfCell _cell;
    private OdfCellFontProxy? _font;
    private OdfCellFillProxy? _fill;

    /// <summary>
    /// 初始化 <see cref="OdfCellStyleProxy"/> 類別的新執行個體。
    /// </summary>
    /// <param name="cell">目標儲存格</param>
    public OdfCellStyleProxy(OdfCell cell)
    {
        _cell = cell ?? throw new ArgumentNullException(nameof(cell));
    }

    /// <summary>
    /// 取得此儲存格的字型樣式代理。
    /// </summary>
    public OdfCellFontProxy Font => _font ??= new OdfCellFontProxy(_cell);

    /// <summary>
    /// 取得此儲存格的填充背景樣式代理。
    /// </summary>
    public OdfCellFillProxy Fill => _fill ??= new OdfCellFillProxy(_cell);

    /// <summary>
    /// 取得或設定此儲存格套用的資料數值格式（例如 <c>data-style-name</c>）。
    /// </summary>
    public string? NumberFormat
    {
        get => _cell.Node.GetAttribute("data-style-name", OdfNamespaces.Office);
        set
        {
            if (string.IsNullOrEmpty(value))
                _cell.Node.RemoveAttribute("data-style-name", OdfNamespaces.Office);
            else
                _cell.Node.SetAttribute("data-style-name", OdfNamespaces.Office, value!, "office");
        }
    }
}

/// <summary>
/// 實作試算表儲存格的字型樣式代理。
/// </summary>
public sealed class OdfCellFontProxy
{
    private readonly OdfCell _cell;

    internal OdfCellFontProxy(OdfCell cell)
    {
        _cell = cell;
    }

    /// <summary>
    /// 取得或設定字型是否為粗體。
    /// </summary>
    public bool IsBold
    {
        get => _cell.Document.StyleEngine.GetStyleProperty(_cell.StyleName ?? string.Empty, "font-weight", OdfNamespaces.Fo, "text") == "bold";
        set => _cell.Document.StyleEngine.SetLocalStyleProperty(_cell.Node, "table-cell", "text-properties", "font-weight", OdfNamespaces.Fo, value ? "bold" : "normal", "fo");
    }

    /// <summary>
    /// 取得或設定字型是否為斜體。
    /// </summary>
    public bool IsItalic
    {
        get => _cell.Document.StyleEngine.GetStyleProperty(_cell.StyleName ?? string.Empty, "font-style", OdfNamespaces.Fo, "text") == "italic";
        set => _cell.Document.StyleEngine.SetLocalStyleProperty(_cell.Node, "table-cell", "text-properties", "font-style", OdfNamespaces.Fo, value ? "italic" : "normal", "fo");
    }

    /// <summary>
    /// 取得或設定字型大小（例如 <c>12pt</c>）。
    /// </summary>
    public string? Size
    {
        get => _cell.Document.StyleEngine.GetStyleProperty(_cell.StyleName ?? string.Empty, "font-size", OdfNamespaces.Fo, "text");
        set => _cell.Document.StyleEngine.SetLocalStyleProperty(_cell.Node, "table-cell", "text-properties", "font-size", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 取得或設定字型顏色（例如十六進位色碼 <c>#FF0000</c>）。
    /// </summary>
    public string? Color
    {
        get => _cell.Document.StyleEngine.GetStyleProperty(_cell.StyleName ?? string.Empty, "color", OdfNamespaces.Fo, "text");
        set => _cell.Document.StyleEngine.SetLocalStyleProperty(_cell.Node, "table-cell", "text-properties", "color", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }
}

/// <summary>
/// 實作試算表儲存格的填充背景樣式代理。
/// </summary>
public sealed class OdfCellFillProxy
{
    private readonly OdfCell _cell;

    internal OdfCellFillProxy(OdfCell cell)
    {
        _cell = cell;
    }

    /// <summary>
    /// 取得或設定儲存格的背景填充顏色（例如 <c>#FFFF00</c>）。
    /// </summary>
    public string? Color
    {
        get => _cell.Document.StyleEngine.GetStyleProperty(_cell.StyleName ?? string.Empty, "background-color", OdfNamespaces.Fo, "table-cell");
        set => _cell.Document.StyleEngine.SetLocalStyleProperty(_cell.Node, "table-cell", "table-cell-properties", "background-color", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }
}
