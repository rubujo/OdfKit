namespace OdfKit.Styles;

/// <summary>
/// 表示高階 builder 可共用的文件設計主題。
/// </summary>
public sealed class OdfDesignTheme
{
    private readonly string[] _accentFillColors;

    /// <summary>
    /// 初始化 <see cref="OdfDesignTheme"/> 類別的新執行個體。
    /// </summary>
    public OdfDesignTheme()
    {
        _accentFillColors = ["#D9EAF7", "#FFF2CC", "#D9EAD3", "#EADCF8"];
    }

    private OdfDesignTheme(
        string name,
        string strokeColor,
        string connectorColor,
        string[] accentFillColors)
    {
        Name = name;
        StrokeColor = strokeColor;
        ConnectorColor = connectorColor;
        _accentFillColors = accentFillColors;
    }

    /// <summary>
    /// 取得適合流程圖與架構圖的內建主題。
    /// </summary>
    public static OdfDesignTheme Flowchart => new(
        "Flowchart",
        "#274E13",
        "#073763",
        ["#CFE2F3", "#FFF2CC", "#D9EAD3", "#EADCF8"]);

    /// <summary>
    /// 取得或設定主題名稱。
    /// </summary>
    public string Name { get; set; } = "Default";

    /// <summary>
    /// 取得或設定一般圖形的預設邊框色彩。
    /// </summary>
    public string StrokeColor { get; set; } = "#333333";

    /// <summary>
    /// 取得或設定連接線與線段的預設色彩。
    /// </summary>
    public string ConnectorColor { get; set; } = "#333333";

    /// <summary>
    /// 依序號取得圖形填滿色彩。
    /// </summary>
    /// <param name="index">圖形序號</param>
    /// <returns>對應的填滿色彩</returns>
    public string GetAccentFillColor(int index)
    {
        int normalized = Math.Abs(index) % _accentFillColors.Length;
        return _accentFillColors[normalized];
    }

    /// <summary>
    /// 取代主題使用的強調填滿色彩序列。
    /// </summary>
    /// <param name="colors">色彩序列</param>
    /// <returns>目前主題執行個體</returns>
    public OdfDesignTheme WithAccentFillColors(params string[] colors)
    {
        if (colors is null)
            throw new ArgumentNullException(nameof(colors));
        if (colors.Length == 0)
            return this;

        Array.Copy(colors, _accentFillColors, Math.Min(colors.Length, _accentFillColors.Length));
        return this;
    }
}
