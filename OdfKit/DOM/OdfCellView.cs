using System;
using System.Runtime.InteropServices;

namespace OdfKit.DOM;

/// <summary>
/// 表示輕量儲存格檢視的資料種類。
/// </summary>
public enum OdfCellDataKind
{
    /// <summary>
    /// 空白或未指定的儲存格。
    /// </summary>
    Empty = 0,

    /// <summary>
    /// 數值儲存格。
    /// </summary>
    Number = 1,

    /// <summary>
    /// 布林值儲存格。
    /// </summary>
    Boolean = 2,

    /// <summary>
    /// 日期時間儲存格。
    /// </summary>
    DateTime = 3,

    /// <summary>
    /// 文字儲存格。
    /// </summary>
    Text = 4,
}

/// <summary>
/// 表示不建立儲存格 DOM facade 即可讀取的緊湊儲存格資料。
/// </summary>
public readonly struct OdfCellData
{
    private readonly OdfCellDataKind _kind;
    private readonly OdfCellPrimitiveData _primitive;
    private readonly string? _text;
    private readonly string? _styleName;
    private readonly string? _formula;

    private OdfCellData(OdfCellDataKind kind, OdfCellPrimitiveData primitive, string? text, string? styleName, string? formula)
    {
        _kind = kind;
        _primitive = primitive;
        _text = text;
        _styleName = styleName;
        _formula = formula;
    }

    /// <summary>
    /// 取得儲存格資料種類。
    /// </summary>
    public OdfCellDataKind Kind => _kind;

    /// <summary>
    /// 取得數值資料；僅當 <see cref="Kind"/> 為 <see cref="OdfCellDataKind.Number"/> 時有效。
    /// </summary>
    public double Number => _primitive.Number;

    /// <summary>
    /// 取得布林資料；僅當 <see cref="Kind"/> 為 <see cref="OdfCellDataKind.Boolean"/> 時有效。
    /// </summary>
    public bool Boolean => _primitive.Boolean != 0;

    /// <summary>
    /// 取得日期時間資料；僅當 <see cref="Kind"/> 為 <see cref="OdfCellDataKind.DateTime"/> 時有效。
    /// </summary>
    public DateTime DateTime => new(_primitive.Ticks);

    /// <summary>
    /// 取得文字資料。
    /// </summary>
    public string? Text => _text;

    /// <summary>
    /// 取得樣式名稱。
    /// </summary>
    public string? StyleName => _styleName;

    /// <summary>
    /// 取得 OpenFormula 公式字串。
    /// </summary>
    public string? Formula => _formula;

    /// <summary>
    /// 建立空白儲存格資料。
    /// </summary>
    /// <param name="styleName">樣式名稱</param>
    /// <param name="formula">公式字串</param>
    /// <returns>空白儲存格資料</returns>
    public static OdfCellData Empty(string? styleName = null, string? formula = null)
        => new(OdfCellDataKind.Empty, default, null, styleName, formula);

    /// <summary>
    /// 建立數值儲存格資料。
    /// </summary>
    /// <param name="value">數值</param>
    /// <param name="styleName">樣式名稱</param>
    /// <param name="formula">公式字串</param>
    /// <returns>數值儲存格資料</returns>
    public static OdfCellData FromNumber(double value, string? styleName = null, string? formula = null)
        => new(OdfCellDataKind.Number, OdfCellPrimitiveData.FromNumber(value), null, styleName, formula);

    /// <summary>
    /// 建立布林儲存格資料。
    /// </summary>
    /// <param name="value">布林值</param>
    /// <param name="styleName">樣式名稱</param>
    /// <param name="formula">公式字串</param>
    /// <returns>布林儲存格資料</returns>
    public static OdfCellData FromBoolean(bool value, string? styleName = null, string? formula = null)
        => new(OdfCellDataKind.Boolean, OdfCellPrimitiveData.FromBoolean(value), null, styleName, formula);

    /// <summary>
    /// 建立日期時間儲存格資料。
    /// </summary>
    /// <param name="value">日期時間</param>
    /// <param name="styleName">樣式名稱</param>
    /// <param name="formula">公式字串</param>
    /// <returns>日期時間儲存格資料</returns>
    public static OdfCellData FromDateTime(DateTime value, string? styleName = null, string? formula = null)
        => new(OdfCellDataKind.DateTime, OdfCellPrimitiveData.FromTicks(value.Ticks), null, styleName, formula);

    /// <summary>
    /// 建立文字儲存格資料。
    /// </summary>
    /// <param name="value">文字值</param>
    /// <param name="styleName">樣式名稱</param>
    /// <param name="formula">公式字串</param>
    /// <returns>文字儲存格資料</returns>
    public static OdfCellData FromText(string? value, string? styleName = null, string? formula = null)
        => new(OdfCellDataKind.Text, default, value, styleName, formula);

    [StructLayout(LayoutKind.Explicit)]
    private struct OdfCellPrimitiveData
    {
        [FieldOffset(0)]
        private double _number;

        [FieldOffset(0)]
        private long _ticks;

        [FieldOffset(0)]
        private byte _boolean;

        public double Number => _number;

        public long Ticks => _ticks;

        public byte Boolean => _boolean;

        public static OdfCellPrimitiveData FromNumber(double value)
        {
            OdfCellPrimitiveData data = default;
            data._number = value;
            return data;
        }

        public static OdfCellPrimitiveData FromTicks(long value)
        {
            OdfCellPrimitiveData data = default;
            data._ticks = value;
            return data;
        }

        public static OdfCellPrimitiveData FromBoolean(bool value)
        {
            OdfCellPrimitiveData data = default;
            data._boolean = value ? (byte)1 : (byte)0;
            return data;
        }
    }
}

/// <summary>
/// 表示表格中單一儲存格的輕量檢視。
/// </summary>
public readonly struct OdfCellView
{
    internal OdfCellView(int rowIndex, int columnIndex, OdfCellData data)
    {
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        Data = data;
    }

    /// <summary>
    /// 取得以零為基準的列索引。
    /// </summary>
    public int RowIndex { get; }

    /// <summary>
    /// 取得以零為基準的欄索引。
    /// </summary>
    public int ColumnIndex { get; }

    /// <summary>
    /// 取得儲存格資料。
    /// </summary>
    public OdfCellData Data { get; }
}
