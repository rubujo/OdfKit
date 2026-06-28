using System.Collections.Generic;
using OdfKit.Styles;
using OdfKit.Text;

namespace OdfKit.Presentation;

/// <summary>
/// 提供 <see cref="PresentationDocument"/> 的 Fluent 建立 API。
/// </summary>
public sealed class PresentationDocumentBuilder
{
    private readonly PresentationDocument _document;
    private OdfDesignTheme _theme = new();
    private OdfLayoutPreset _layoutPreset = OdfLayoutPreset.BusinessDeck;
    private int _slideCount;
    private string? _defaultMasterPageName;

    internal PresentationDocumentBuilder(PresentationDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 設定文件中繼資料。
    /// </summary>
    /// <param name="configure">中繼資料設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public PresentationDocumentBuilder WithMetadata(Action<TextDocumentMetadataBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        configure(new TextDocumentMetadataBuilder(new OdfDocumentMetadata(_document)));
        return this;
    }

    /// <summary>
    /// 設定後續投影片與圖形使用的設計主題。
    /// </summary>
    /// <param name="theme">設計主題</param>
    /// <returns>目前 builder 執行個體</returns>
    public PresentationDocumentBuilder WithTheme(OdfDesignTheme theme)
    {
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        return this;
    }

    /// <summary>
    /// 設定後續投影片與圖形使用的樣式集合。
    /// </summary>
    /// <param name="styles">樣式集合</param>
    /// <returns>目前 builder 執行個體</returns>
    public PresentationDocumentBuilder WithStyles(OdfStyleSet styles)
    {
        ApplyStyleSetToTheme(styles ?? throw new ArgumentNullException(nameof(styles)));
        return this;
    }

    /// <summary>
    /// 設定後續投影片與圖形使用的樣式集合。
    /// </summary>
    /// <param name="configure">樣式集合設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public PresentationDocumentBuilder WithStyles(Action<OdfStyleSet> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        var styles = new OdfStyleSet();
        configure(styles);
        return WithStyles(styles);
    }

    /// <summary>
    /// 設定後續商業簡報 helper 使用的版面 preset。
    /// </summary>
    /// <param name="preset">版面 preset</param>
    /// <returns>目前 builder 執行個體</returns>
    public PresentationDocumentBuilder WithLayoutPreset(OdfLayoutPreset preset)
    {
        _layoutPreset = preset ?? throw new ArgumentNullException(nameof(preset));
        return this;
    }

    /// <summary>
    /// 建立後續投影片預設套用的母片。
    /// </summary>
    /// <param name="name">母片名稱</param>
    /// <param name="backgroundColor">母片背景色，例如 <c>#FFFFFF</c></param>
    /// <returns>目前 builder 執行個體</returns>
    public PresentationDocumentBuilder WithMasterPage(string name, string? backgroundColor = null)
    {
        _document.AddMasterPage(name, new OdfMasterPageDefinition
        {
            BackgroundColor = backgroundColor,
        });
        _defaultMasterPageName = name;
        return this;
    }

    /// <summary>
    /// 新增投影片並設定其內容。
    /// </summary>
    /// <param name="name">投影片名稱</param>
    /// <param name="configure">投影片設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public PresentationDocumentBuilder AddSlide(string name, Action<OdfSlideBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        OdfSlide slide = _document.AddSlide(name);
        configure(new OdfSlideBuilder(slide, _theme));
        if (!string.IsNullOrWhiteSpace(_defaultMasterPageName))
        {
            slide.MasterPageName = _defaultMasterPageName!;
        }

        _slideCount++;
        return this;
    }

    /// <summary>
    /// 新增投影片並設定其內容。
    /// </summary>
    /// <param name="configure">投影片設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public PresentationDocumentBuilder AddSlide(Action<OdfSlideBuilder> configure)
    {
        return AddSlide($"Slide {_slideCount + 1}", configure);
    }

    /// <summary>
    /// 新增標題投影片。
    /// </summary>
    /// <param name="name">投影片名稱</param>
    /// <param name="title">標題文字</param>
    /// <param name="subtitle">副標題文字</param>
    /// <param name="configure">其他投影片設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public PresentationDocumentBuilder AddTitleSlide(
        string name,
        string title,
        string? subtitle = null,
        Action<OdfSlideBuilder>? configure = null)
    {
        return AddSlide(name, slide =>
        {
            OdfLayoutBounds titleBounds = _layoutPreset.TitleBounds;
            OdfLayoutBounds subtitleBounds = _layoutPreset.SubtitleBounds;
            slide.WithLayout(OdfPresentationLayout.TitleAndSubtitle)
                .AddTitle(title, titleBounds.XCm, titleBounds.YCm, titleBounds.WidthCm, titleBounds.HeightCm);
            if (!string.IsNullOrEmpty(subtitle))
            {
                slide.AddTextBox(
                    subtitle!,
                    subtitleBounds.XCm,
                    subtitleBounds.YCm,
                    subtitleBounds.WidthCm,
                    subtitleBounds.HeightCm);
            }

            configure?.Invoke(slide);
        });
    }

    /// <summary>
    /// 新增雙欄內容投影片。
    /// </summary>
    /// <param name="name">投影片名稱</param>
    /// <param name="title">標題文字</param>
    /// <param name="leftParagraphs">左欄段落</param>
    /// <param name="rightParagraphs">右欄段落</param>
    /// <param name="configure">其他投影片設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public PresentationDocumentBuilder AddTwoColumnSlide(
        string name,
        string title,
        IEnumerable<string> leftParagraphs,
        IEnumerable<string> rightParagraphs,
        Action<OdfSlideBuilder>? configure = null)
    {
        return AddSlide(name, slide =>
        {
            OdfLayoutBounds titleBounds = _layoutPreset.TitleBounds;
            OdfLayoutBounds left = _layoutPreset.LeftColumnBounds;
            OdfLayoutBounds right = _layoutPreset.RightColumnBounds;
            slide.WithLayout(OdfPresentationLayout.TitleAndBody)
                .AddTitle(title, titleBounds.XCm, titleBounds.YCm, titleBounds.WidthCm, titleBounds.HeightCm)
                .AddTextBox(leftParagraphs, left.XCm, left.YCm, left.WidthCm, left.HeightCm)
                .AddTextBox(rightParagraphs, right.XCm, right.YCm, right.WidthCm, right.HeightCm);
            configure?.Invoke(slide);
        });
    }

    /// <summary>
    /// 新增圖表版面投影片。
    /// </summary>
    /// <param name="name">投影片名稱</param>
    /// <param name="title">標題文字</param>
    /// <param name="configure">其他投影片設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public PresentationDocumentBuilder AddChartSlide(
        string name,
        string title,
        Action<OdfSlideBuilder>? configure = null)
    {
        return AddSlide(name, slide =>
        {
            OdfLayoutBounds titleBounds = _layoutPreset.TitleBounds;
            OdfLayoutBounds chart = _layoutPreset.ChartBounds;
            slide.WithLayout(OdfPresentationLayout.TitleAndBody)
                .AddTitle(title, titleBounds.XCm, titleBounds.YCm, titleBounds.WidthCm, titleBounds.HeightCm)
                .AddChartPlaceholder(chart.XCm, chart.YCm, chart.WidthCm, chart.HeightCm);
            configure?.Invoke(slide);
        });
    }

    /// <summary>
    /// 建立並傳回簡報文件。
    /// </summary>
    /// <returns>建立完成的簡報文件</returns>
    public PresentationDocument Build()
    {
        return _document;
    }

    private void ApplyStyleSetToTheme(OdfStyleSet styles)
    {
        string? strokeColor = styles.HeadingColor ?? styles.BodyColor;
        if (!string.IsNullOrWhiteSpace(strokeColor))
        {
            _theme.StrokeColor = strokeColor!;
            _theme.ConnectorColor = strokeColor!;
        }

        if (!string.IsNullOrWhiteSpace(styles.TableHeaderBackgroundColor))
        {
            _theme.WithAccentFillColors(styles.TableHeaderBackgroundColor!);
        }
    }
}

/// <summary>
/// 提供投影片內容的 Fluent 建立 API。
/// </summary>
public sealed class OdfSlideBuilder
{
    private readonly OdfSlide _slide;
    private readonly OdfDesignTheme _theme;
    private int _shapeCount;

    internal OdfSlideBuilder(OdfSlide slide, OdfDesignTheme theme)
    {
        _slide = slide ?? throw new ArgumentNullException(nameof(slide));
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
    }

    /// <summary>
    /// 新增標題文字方塊。
    /// </summary>
    /// <param name="text">標題文字</param>
    /// <param name="xCm">左側位置（公分）</param>
    /// <param name="yCm">上方位置（公分）</param>
    /// <param name="widthCm">寬度（公分）</param>
    /// <param name="heightCm">高度（公分）</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfSlideBuilder AddTitle(
        string text,
        double xCm = 1,
        double yCm = 1,
        double widthCm = 10,
        double heightCm = 2)
    {
        _slide.AddTextBox(
            OdfLength.FromCentimeters(xCm),
            OdfLength.FromCentimeters(yCm),
            OdfLength.FromCentimeters(widthCm),
            OdfLength.FromCentimeters(heightCm),
            text);
        return this;
    }

    /// <summary>
    /// 新增文字方塊。
    /// </summary>
    /// <param name="text">文字內容</param>
    /// <param name="xCm">左側位置（公分）</param>
    /// <param name="yCm">上方位置（公分）</param>
    /// <param name="widthCm">寬度（公分）</param>
    /// <param name="heightCm">高度（公分）</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfSlideBuilder AddTextBox(
        string text,
        double xCm,
        double yCm,
        double widthCm,
        double heightCm)
    {
        _slide.AddTextBox(
            OdfLength.FromCentimeters(xCm),
            OdfLength.FromCentimeters(yCm),
            OdfLength.FromCentimeters(widthCm),
            OdfLength.FromCentimeters(heightCm),
            text);
        return this;
    }

    /// <summary>
    /// 新增多段落文字方塊。
    /// </summary>
    /// <param name="paragraphs">段落文字集合</param>
    /// <param name="xCm">左側位置（公分）</param>
    /// <param name="yCm">上方位置（公分）</param>
    /// <param name="widthCm">寬度（公分）</param>
    /// <param name="heightCm">高度（公分）</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfSlideBuilder AddTextBox(
        IEnumerable<string> paragraphs,
        double xCm,
        double yCm,
        double widthCm,
        double heightCm)
    {
        _slide.AddTextBox(
            OdfLength.FromCentimeters(xCm),
            OdfLength.FromCentimeters(yCm),
            OdfLength.FromCentimeters(widthCm),
            OdfLength.FromCentimeters(heightCm),
            paragraphs);
        return this;
    }

    /// <summary>
    /// 新增圖片。
    /// </summary>
    /// <param name="imageBytes">圖片位元組</param>
    /// <param name="xCm">左側位置（公分）</param>
    /// <param name="yCm">上方位置（公分）</param>
    /// <param name="widthCm">寬度（公分）</param>
    /// <param name="heightCm">高度（公分）</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfSlideBuilder AddImage(
        byte[] imageBytes,
        double xCm,
        double yCm,
        double widthCm,
        double heightCm)
    {
        _slide.AddPicture(
            imageBytes,
            OdfLength.FromCentimeters(xCm),
            OdfLength.FromCentimeters(yCm),
            OdfLength.FromCentimeters(widthCm),
            OdfLength.FromCentimeters(heightCm));
        return this;
    }

    /// <summary>
    /// 新增基本圖形。
    /// </summary>
    /// <param name="shapeType">圖形類型</param>
    /// <param name="xCm">左側位置（公分）</param>
    /// <param name="yCm">上方位置（公分）</param>
    /// <param name="widthCm">寬度（公分）</param>
    /// <param name="heightCm">高度（公分）</param>
    /// <param name="configure">圖形設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfSlideBuilder AddShape(
        OdfShapeType shapeType,
        double xCm,
        double yCm,
        double widthCm,
        double heightCm,
        Action<OdfPresentationShapeBuilder>? configure = null)
    {
        OdfShape shape = _slide.AddShape(
            shapeType,
            OdfLength.FromCentimeters(xCm),
            OdfLength.FromCentimeters(yCm),
            OdfLength.FromCentimeters(widthCm),
            OdfLength.FromCentimeters(heightCm));
        ApplyThemedShapeStyle(shape);
        configure?.Invoke(new OdfPresentationShapeBuilder(shape));
        return this;
    }

    private void ApplyThemedShapeStyle(OdfShape shape)
    {
        shape.FillColor = _theme.GetAccentFillColor(_shapeCount);
        shape.StrokeColor = _theme.StrokeColor;
        _shapeCount++;
    }

    /// <summary>
    /// 新增圖表預留位置。
    /// </summary>
    /// <param name="xCm">左側位置（公分）</param>
    /// <param name="yCm">上方位置（公分）</param>
    /// <param name="widthCm">寬度（公分）</param>
    /// <param name="heightCm">高度（公分）</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfSlideBuilder AddChartPlaceholder(
        double xCm,
        double yCm,
        double widthCm,
        double heightCm)
    {
        _slide.AddPlaceholder(
            OdfPlaceholderType.Chart,
            OdfLength.FromCentimeters(xCm),
            OdfLength.FromCentimeters(yCm),
            OdfLength.FromCentimeters(widthCm),
            OdfLength.FromCentimeters(heightCm));
        return this;
    }

    /// <summary>
    /// 套用標準投影片版面。
    /// </summary>
    /// <param name="layout">投影片版面</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfSlideBuilder WithLayout(OdfPresentationLayout layout)
    {
        _slide.SetLayout(layout);
        return this;
    }

    /// <summary>
    /// 新增投影片強調動畫。
    /// </summary>
    /// <param name="shapeId">目標圖形識別碼</param>
    /// <param name="effect">動畫效果</param>
    /// <param name="durationSeconds">持續秒數</param>
    /// <param name="trigger">觸發方式</param>
    /// <param name="delaySeconds">延遲秒數</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfSlideBuilder AddEmphasisEffect(
        string shapeId,
        OdfAnimationEffect effect,
        double durationSeconds = 0.5,
        OdfAnimationTrigger trigger = OdfAnimationTrigger.OnClick,
        double delaySeconds = 0)
    {
        _slide.AddEmphasisEffect(shapeId, effect, TimeSpan.FromSeconds(durationSeconds), trigger, TimeSpan.FromSeconds(delaySeconds));
        return this;
    }

    /// <summary>
    /// 新增投影片退場動畫。
    /// </summary>
    /// <param name="shapeId">目標圖形識別碼</param>
    /// <param name="effect">動畫效果</param>
    /// <param name="trigger">觸發方式</param>
    /// <param name="delaySeconds">延遲秒數</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfSlideBuilder AddExitEffect(
        string shapeId,
        OdfAnimationEffect effect,
        OdfAnimationTrigger trigger = OdfAnimationTrigger.AfterPrevious,
        double delaySeconds = 0)
    {
        _slide.AddExitEffect(shapeId, effect, trigger, TimeSpan.FromSeconds(delaySeconds));
        return this;
    }
    /// <summary>
    /// 新增標題預留位置。
    /// </summary>
    /// <param name="xCm">左側位置（公分）</param>
    /// <param name="yCm">上方位置（公分）</param>
    /// <param name="widthCm">寬度（公分）</param>
    /// <param name="heightCm">高度（公分）</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfSlideBuilder AddTitlePlaceholder(
        double xCm = 1,
        double yCm = 1,
        double widthCm = 10,
        double heightCm = 2)
    {
        _slide.AddPlaceholder(
            OdfPlaceholderType.Title,
            OdfLength.FromCentimeters(xCm),
            OdfLength.FromCentimeters(yCm),
            OdfLength.FromCentimeters(widthCm),
            OdfLength.FromCentimeters(heightCm));
        return this;
    }

    /// <summary>
    /// 設定講者備忘文字。
    /// </summary>
    /// <param name="notes">講者備忘內容</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfSlideBuilder WithSpeakerNotes(string notes)
    {
        _slide.SpeakerNotes = notes;
        return this;
    }

    /// <summary>
    /// 設定多段落講者備忘文字。
    /// </summary>
    /// <param name="paragraphs">講者備忘段落集合</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfSlideBuilder WithSpeakerNotes(IEnumerable<string> paragraphs)
    {
        _slide.SetSpeakerNotes(paragraphs);
        return this;
    }

    /// <summary>
    /// 設定投影片切換效果。
    /// </summary>
    /// <param name="type">切換類型</param>
    /// <param name="durationPoints">持續時間（點）</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfSlideBuilder WithTransition(OdfTransitionType type, double durationPoints = 72)
    {
        _slide.SetTransition(type, OdfLength.FromPoints(durationPoints));
        return this;
    }

    /// <summary>
    /// 為指定圖形新增進場動畫。
    /// </summary>
    /// <param name="shapeId">目標圖形識別碼</param>
    /// <param name="effect">動畫效果</param>
    /// <param name="trigger">觸發方式</param>
    /// <param name="delaySeconds">延遲秒數</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfSlideBuilder AddEntranceEffect(
        string shapeId,
        OdfAnimationEffect effect,
        OdfAnimationTrigger trigger = OdfAnimationTrigger.OnClick,
        double delaySeconds = 0)
    {
        _slide.AddEntranceEffect(shapeId, effect, trigger, TimeSpan.FromSeconds(delaySeconds));
        return this;
    }
}

/// <summary>
/// 提供簡報圖形的 Fluent 設定 API。
/// </summary>
public sealed class OdfPresentationShapeBuilder
{
    private readonly OdfShape _shape;

    internal OdfPresentationShapeBuilder(OdfShape shape)
    {
        _shape = shape ?? throw new ArgumentNullException(nameof(shape));
    }

    /// <summary>
    /// 設定圖形識別碼。
    /// </summary>
    /// <param name="id">圖形識別碼</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfPresentationShapeBuilder WithId(string id)
    {
        _shape.Id = id;
        return this;
    }

    /// <summary>
    /// 設定填滿色彩。
    /// </summary>
    /// <param name="color">色彩值</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfPresentationShapeBuilder Fill(string color)
    {
        _shape.FillColor = color;
        return this;
    }

    /// <summary>
    /// 設定線條色彩。
    /// </summary>
    /// <param name="color">色彩值</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfPresentationShapeBuilder Stroke(string color)
    {
        _shape.StrokeColor = color;
        return this;
    }
}
