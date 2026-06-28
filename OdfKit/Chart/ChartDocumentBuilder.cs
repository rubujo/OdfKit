using System;
using OdfKit.Spreadsheet;
using OdfKit.Styles;

namespace OdfKit.Chart;

/// <summary>
/// 提供高階圖表文件的 Fluent 建構流程。
/// </summary>
public sealed class ChartDocumentBuilder
{
    private readonly ChartDocument _document;
    private OdfStyleSet? _styles;

    internal ChartDocumentBuilder(ChartDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 設定圖表類型。
    /// </summary>
    /// <param name="chartType">圖表類型</param>
    /// <returns>目前 builder</returns>
    public ChartDocumentBuilder WithType(OdfChartType chartType)
    {
        _document.ChartClass = chartType switch
        {
            OdfChartType.Line => "chart:line",
            OdfChartType.Pie => "chart:pie",
            OdfChartType.Area => "chart:area",
            OdfChartType.Scatter => "chart:scatter",
            OdfChartType.Bubble => "chart:bubble",
            _ => "chart:bar"
        };

        return this;
    }

    /// <summary>
    /// 設定圖表標題。
    /// </summary>
    /// <param name="title">圖表標題</param>
    /// <returns>目前 builder</returns>
    public ChartDocumentBuilder WithTitle(string? title)
    {
        _document.ChartTitle = title;
        return this;
    }

    /// <summary>
    /// 設定此 builder 後續建立圖表序列會套用的樣式集合。
    /// </summary>
    /// <param name="styles">樣式集合</param>
    /// <returns>目前 builder</returns>
    public ChartDocumentBuilder WithStyles(OdfStyleSet styles)
    {
        _styles = styles ?? throw new ArgumentNullException(nameof(styles));
        ApplyStyleSetToAllSeries();
        return this;
    }

    /// <summary>
    /// 設定此 builder 後續建立圖表序列會套用的樣式集合。
    /// </summary>
    /// <param name="configure">樣式集合設定委派</param>
    /// <returns>目前 builder</returns>
    public ChartDocumentBuilder WithStyles(Action<OdfStyleSet> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var styles = new OdfStyleSet();
        configure(styles);
        return WithStyles(styles);
    }

    /// <summary>
    /// 設定圖表資料範圍。
    /// </summary>
    /// <param name="sheetName">工作表名稱</param>
    /// <param name="range">儲存格範圍</param>
    /// <param name="firstRowAsHeader">首列是否視為標題列</param>
    /// <param name="firstColumnAsLabel">首欄是否視為分類標籤</param>
    /// <returns>目前 builder</returns>
    public ChartDocumentBuilder WithDataRange(
        string sheetName,
        OdfCellRange range,
        bool firstRowAsHeader = true,
        bool firstColumnAsLabel = true)
    {
        _document.SetDataRange(sheetName, range, firstRowAsHeader, firstColumnAsLabel);
        ApplyStyleSetToAllSeries();
        return this;
    }

    /// <summary>
    /// 設定圖例配置。
    /// </summary>
    /// <param name="position">圖例位置；空白表示隱藏圖例</param>
    /// <param name="alignment">圖例對齊方式</param>
    /// <returns>目前 builder</returns>
    public ChartDocumentBuilder WithLegend(string? position = "end", string? alignment = null)
    {
        _document.Legend.Position = position;
        _document.Legend.Alignment = alignment;
        return this;
    }

    /// <summary>
    /// 設定指定維度座標軸。
    /// </summary>
    /// <param name="dimension">座標軸維度，例如 x、y、z</param>
    /// <param name="configure">座標軸配置委派</param>
    /// <returns>目前 builder</returns>
    public ChartDocumentBuilder WithAxis(string dimension, Action<ChartAxisBuilder> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        configure(new ChartAxisBuilder(_document, dimension));
        return this;
    }

    /// <summary>
    /// 設定指定資料序列。
    /// </summary>
    /// <param name="index">序列索引（從 0 起算）</param>
    /// <param name="configure">序列配置委派</param>
    /// <returns>目前 builder</returns>
    public ChartDocumentBuilder ConfigureSeries(int index, Action<ChartSeriesBuilder> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        OdfChartSeries series = _document.GetSeriesEditor(index);
        ApplyStyleSetToSeries(series, index);
        configure(new ChartSeriesBuilder(series));
        return this;
    }

    /// <summary>
    /// 完成建構並回傳圖表文件。
    /// </summary>
    /// <returns>建構完成的 <see cref="ChartDocument"/></returns>
    public ChartDocument Build() => _document;

    private void ApplyStyleSetToAllSeries()
    {
        if (_styles is null)
        {
            return;
        }

        for (int i = 0; i < _document.SeriesCount; i++)
        {
            ApplyStyleSetToSeries(_document.GetSeriesEditor(i), i);
        }
    }

    private void ApplyStyleSetToSeries(OdfChartSeries series, int index)
    {
        if (_styles is null)
        {
            return;
        }

        series.Style.FillColor = _styles.GetChartPaletteColor(index);
    }
}

/// <summary>
/// 圖表座標軸的 Fluent 配置器。
/// </summary>
public sealed class ChartAxisBuilder
{
    private readonly OdfChartDocument _document;
    private readonly string _dimension;

    internal ChartAxisBuilder(OdfChartDocument document, string dimension)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _dimension = dimension;
    }

    /// <summary>
    /// 設定座標軸標題。
    /// </summary>
    /// <param name="title">標題文字</param>
    /// <returns>目前 builder</returns>
    public ChartAxisBuilder WithTitle(string? title)
    {
        _document.SetAxisTitle(_dimension, title);
        return this;
    }

    /// <summary>
    /// 設定最小值。
    /// </summary>
    /// <param name="value">最小值</param>
    /// <returns>目前 builder</returns>
    public ChartAxisBuilder WithMinimum(double? value)
    {
        _document.SetAxisMinimum(_dimension, value);
        return this;
    }

    /// <summary>
    /// 設定最大值。
    /// </summary>
    /// <param name="value">最大值</param>
    /// <returns>目前 builder</returns>
    public ChartAxisBuilder WithMaximum(double? value)
    {
        _document.SetAxisMaximum(_dimension, value);
        return this;
    }

    /// <summary>
    /// 設定是否使用對數刻度。
    /// </summary>
    /// <param name="value">對數刻度設定</param>
    /// <returns>目前 builder</returns>
    public ChartAxisBuilder WithLogarithmic(bool? value)
    {
        _document.SetAxisLogarithmic(_dimension, value);
        return this;
    }
}

/// <summary>
/// 圖表資料序列的 Fluent 配置器。
/// </summary>
public sealed class ChartSeriesBuilder
{
    private readonly OdfChartSeries _series;

    internal ChartSeriesBuilder(OdfChartSeries series)
    {
        _series = series ?? throw new ArgumentNullException(nameof(series));
    }

    /// <summary>
    /// 配置序列樣式。
    /// </summary>
    /// <param name="configure">樣式配置委派</param>
    /// <returns>目前 builder</returns>
    public ChartSeriesBuilder WithStyle(Action<OdfChartStyle> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        configure(_series.Style);
        return this;
    }

    /// <summary>
    /// 依常用預設組合設定序列資料標籤。
    /// </summary>
    /// <param name="preset">資料標籤預設組合；<see cref="OdfChartDataLabelPreset.None"/> 表示移除既有設定</param>
    /// <returns>目前 builder</returns>
    public ChartSeriesBuilder WithDataLabels(OdfChartDataLabelPreset preset)
    {
        _series.SetDataLabelPreset(preset);
        return this;
    }

    /// <summary>
    /// 設定序列資料標籤。
    /// </summary>
    /// <param name="info">資料標籤設定；傳入 <see langword="null"/> 表示移除既有設定</param>
    /// <returns>目前 builder</returns>
    public ChartSeriesBuilder WithDataLabels(OdfChartDataLabelInfo? info)
    {
        _series.SetDataLabels(info);
        return this;
    }

    /// <summary>
    /// 設定序列誤差棒。
    /// </summary>
    /// <param name="info">誤差棒設定</param>
    /// <returns>目前 builder</returns>
    public ChartSeriesBuilder WithErrorIndicator(OdfChartErrorIndicatorInfo info)
    {
        _series.SetErrorIndicator(info);
        return this;
    }
}
