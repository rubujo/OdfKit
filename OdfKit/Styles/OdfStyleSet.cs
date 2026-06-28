namespace OdfKit.Styles;

/// <summary>
/// 表示高階文件 builder 可共用的樣式集合。
/// </summary>
public sealed class OdfStyleSet
{
    private readonly string[] _chartPaletteColors = ["#4472C4", "#ED7D31", "#A5A5A5", "#70AD47"];

    /// <summary>
    /// 取得適合商業報告的內建樣式集合。
    /// </summary>
    public static OdfStyleSet BusinessReport => new OdfStyleSet()
        .WithHeadingColor("#1F4E79")
        .WithHeadingFontSize(18)
        .WithBodyColor("#222222")
        .WithBodyFontSize(11)
        .WithTableHeaderBackgroundColor("#D9EAF7")
        .WithTableHeaderColor("#111111");

    /// <summary>
    /// 從設計主題建立適合 ODT / ODS 內容 builder 使用的樣式集合。
    /// </summary>
    /// <param name="theme">設計主題</param>
    /// <returns>由設計主題推導出的樣式集合</returns>
    public static OdfStyleSet FromTheme(OdfDesignTheme theme)
    {
        if (theme is null)
            throw new ArgumentNullException(nameof(theme));

        return new OdfStyleSet()
            .WithHeadingColor(theme.StrokeColor)
            .WithTableHeaderBackgroundColor(theme.GetAccentFillColor(0))
            .WithTableHeaderColor(theme.ConnectorColor)
            .WithChartPaletteColors(
                theme.GetAccentFillColor(0),
                theme.GetAccentFillColor(1),
                theme.GetAccentFillColor(2),
                theme.GetAccentFillColor(3));
    }

    /// <summary>
    /// 取得或設定標題文字色彩。
    /// </summary>
    public string? HeadingColor { get; set; }

    /// <summary>
    /// 取得或設定標題文字大小（點）。
    /// </summary>
    public double? HeadingFontSizePoints { get; set; }

    /// <summary>
    /// 取得或設定內文文字色彩。
    /// </summary>
    public string? BodyColor { get; set; }

    /// <summary>
    /// 取得或設定內文文字大小（點）。
    /// </summary>
    public double? BodyFontSizePoints { get; set; }

    /// <summary>
    /// 取得或設定表格首列背景色彩。
    /// </summary>
    public string? TableHeaderBackgroundColor { get; set; }

    /// <summary>
    /// 取得或設定表格首列文字色彩。
    /// </summary>
    public string? TableHeaderColor { get; set; }

    /// <summary>
    /// 取得或設定表格首列文字是否使用粗體。
    /// </summary>
    public bool TableHeaderBold { get; set; } = true;

    /// <summary>
    /// 設定標題文字色彩。
    /// </summary>
    /// <param name="color">色彩值</param>
    /// <returns>目前樣式集合執行個體</returns>
    public OdfStyleSet WithHeadingColor(string? color)
    {
        HeadingColor = color;
        return this;
    }

    /// <summary>
    /// 設定標題文字大小。
    /// </summary>
    /// <param name="points">字級點數</param>
    /// <returns>目前樣式集合執行個體</returns>
    public OdfStyleSet WithHeadingFontSize(double? points)
    {
        HeadingFontSizePoints = points;
        return this;
    }

    /// <summary>
    /// 設定內文文字色彩。
    /// </summary>
    /// <param name="color">色彩值</param>
    /// <returns>目前樣式集合執行個體</returns>
    public OdfStyleSet WithBodyColor(string? color)
    {
        BodyColor = color;
        return this;
    }

    /// <summary>
    /// 設定內文文字大小。
    /// </summary>
    /// <param name="points">字級點數</param>
    /// <returns>目前樣式集合執行個體</returns>
    public OdfStyleSet WithBodyFontSize(double? points)
    {
        BodyFontSizePoints = points;
        return this;
    }

    /// <summary>
    /// 設定表格首列背景色彩。
    /// </summary>
    /// <param name="color">色彩值</param>
    /// <returns>目前樣式集合執行個體</returns>
    public OdfStyleSet WithTableHeaderBackgroundColor(string? color)
    {
        TableHeaderBackgroundColor = color;
        return this;
    }

    /// <summary>
    /// 設定表格首列文字色彩。
    /// </summary>
    /// <param name="color">色彩值</param>
    /// <returns>目前樣式集合執行個體</returns>
    public OdfStyleSet WithTableHeaderColor(string? color)
    {
        TableHeaderColor = color;
        return this;
    }

    /// <summary>
    /// 設定表格首列文字是否使用粗體。
    /// </summary>
    /// <param name="enabled">若要使用粗體則為 <see langword="true"/></param>
    /// <returns>目前樣式集合執行個體</returns>
    public OdfStyleSet WithTableHeaderBold(bool enabled)
    {
        TableHeaderBold = enabled;
        return this;
    }

    /// <summary>
    /// 依序列索引取得圖表色盤色彩。
    /// </summary>
    /// <param name="index">序列索引</param>
    /// <returns>對應的圖表色彩</returns>
    public string GetChartPaletteColor(int index)
    {
        int normalized = Math.Abs(index) % _chartPaletteColors.Length;
        return _chartPaletteColors[normalized];
    }

    /// <summary>
    /// 取代圖表序列使用的色彩序列。
    /// </summary>
    /// <param name="colors">色彩序列</param>
    /// <returns>目前樣式集合執行個體</returns>
    public OdfStyleSet WithChartPaletteColors(params string[] colors)
    {
        if (colors is null)
            throw new ArgumentNullException(nameof(colors));
        if (colors.Length == 0)
            return this;

        Array.Copy(colors, _chartPaletteColors, Math.Min(colors.Length, _chartPaletteColors.Length));
        return this;
    }
}
