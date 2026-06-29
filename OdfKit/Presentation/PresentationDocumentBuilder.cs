using System.Collections.Generic;
using OdfKit.Styles;
using OdfKit.Text;

namespace OdfKit.Presentation;

/// <summary>
/// Provides a fluent builder API for <see cref="PresentationDocument"/>.
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
    /// Configures document metadata.
    /// 設定文件中繼資料。
    /// </summary>
    /// <param name="configure">The metadata configuration delegate. / 中繼資料設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public PresentationDocumentBuilder WithMetadata(Action<TextDocumentMetadataBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        configure(new TextDocumentMetadataBuilder(new OdfDocumentMetadata(_document)));
        return this;
    }

    /// <summary>
    /// Configures the design theme used by subsequent slides and shapes.
    /// 設定後續投影片與圖形使用的設計主題。
    /// </summary>
    /// <param name="theme">The design theme. / 設計主題。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public PresentationDocumentBuilder WithTheme(OdfDesignTheme theme)
    {
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        return this;
    }

    /// <summary>
    /// Configures the style set used by subsequent slides and shapes.
    /// 設定後續投影片與圖形使用的樣式集合。
    /// </summary>
    /// <param name="styles">The style set. / 樣式集合。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public PresentationDocumentBuilder WithStyles(OdfStyleSet styles)
    {
        ApplyStyleSetToTheme(styles ?? throw new ArgumentNullException(nameof(styles)));
        return this;
    }

    /// <summary>
    /// Configures the style set used by subsequent slides and shapes.
    /// 設定後續投影片與圖形使用的樣式集合。
    /// </summary>
    /// <param name="configure">The style set configuration delegate. / 樣式集合設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public PresentationDocumentBuilder WithStyles(Action<OdfStyleSet> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        var styles = new OdfStyleSet();
        configure(styles);
        return WithStyles(styles);
    }

    /// <summary>
    /// Configures the layout preset used by subsequent business presentation helpers.
    /// 設定後續商業簡報 helper 使用的版面 preset。
    /// </summary>
    /// <param name="preset">The layout preset. / 版面 preset。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public PresentationDocumentBuilder WithLayoutPreset(OdfLayoutPreset preset)
    {
        _layoutPreset = preset ?? throw new ArgumentNullException(nameof(preset));
        return this;
    }

    /// <summary>
    /// Creates the master page applied to subsequent slides by default.
    /// 建立後續投影片預設套用的母片。
    /// </summary>
    /// <param name="name">The master page name. / 母片名稱。</param>
    /// <param name="backgroundColor">The master page background color, such as <c>#FFFFFF</c>. / 母片背景色，例如 <c>#FFFFFF</c>。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
    /// Adds a slide and configures its content.
    /// 新增投影片並設定其內容。
    /// </summary>
    /// <param name="name">The slide name. / 投影片名稱。</param>
    /// <param name="configure">The slide configuration delegate. / 投影片設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
    /// Adds a slide and configures its content.
    /// 新增投影片並設定其內容。
    /// </summary>
    /// <param name="configure">The slide configuration delegate. / 投影片設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public PresentationDocumentBuilder AddSlide(Action<OdfSlideBuilder> configure)
    {
        return AddSlide($"Slide {_slideCount + 1}", configure);
    }

    /// <summary>
    /// Adds a title slide.
    /// 新增標題投影片。
    /// </summary>
    /// <param name="name">The slide name. / 投影片名稱。</param>
    /// <param name="title">The title text. / 標題文字。</param>
    /// <param name="subtitle">The subtitle text. / 副標題文字。</param>
    /// <param name="configure">The additional slide configuration delegate. / 其他投影片設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
    /// Adds a two-column content slide.
    /// 新增雙欄內容投影片。
    /// </summary>
    /// <param name="name">The slide name. / 投影片名稱。</param>
    /// <param name="title">The title text. / 標題文字。</param>
    /// <param name="leftParagraphs">The left-column paragraphs. / 左欄段落。</param>
    /// <param name="rightParagraphs">The right-column paragraphs. / 右欄段落。</param>
    /// <param name="configure">The additional slide configuration delegate. / 其他投影片設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
    /// Adds a chart-layout slide.
    /// 新增圖表版面投影片。
    /// </summary>
    /// <param name="name">The slide name. / 投影片名稱。</param>
    /// <param name="title">The title text. / 標題文字。</param>
    /// <param name="configure">The additional slide configuration delegate. / 其他投影片設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
    /// Builds and returns the presentation document.
    /// 建立並傳回簡報文件。
    /// </summary>
    /// <returns>The built presentation document. / 建立完成的簡報文件。</returns>
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
/// Provides a fluent builder API for slide content.
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
    /// Adds a title text box.
    /// 新增標題文字方塊。
    /// </summary>
    /// <param name="text">The title text. / 標題文字。</param>
    /// <param name="xCm">The left position in centimeters. / 左側位置，單位為公分。</param>
    /// <param name="yCm">The top position in centimeters. / 上方位置，單位為公分。</param>
    /// <param name="widthCm">The width in centimeters. / 寬度，單位為公分。</param>
    /// <param name="heightCm">The height in centimeters. / 高度，單位為公分。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
    /// Adds a text box.
    /// 新增文字方塊。
    /// </summary>
    /// <param name="text">The text content. / 文字內容。</param>
    /// <param name="xCm">The left position in centimeters. / 左側位置，單位為公分。</param>
    /// <param name="yCm">The top position in centimeters. / 上方位置，單位為公分。</param>
    /// <param name="widthCm">The width in centimeters. / 寬度，單位為公分。</param>
    /// <param name="heightCm">The height in centimeters. / 高度，單位為公分。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
    /// Adds a multi-paragraph text box.
    /// 新增多段落文字方塊。
    /// </summary>
    /// <param name="paragraphs">The paragraph text collection. / 段落文字集合。</param>
    /// <param name="xCm">The left position in centimeters. / 左側位置，單位為公分。</param>
    /// <param name="yCm">The top position in centimeters. / 上方位置，單位為公分。</param>
    /// <param name="widthCm">The width in centimeters. / 寬度，單位為公分。</param>
    /// <param name="heightCm">The height in centimeters. / 高度，單位為公分。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
    /// Adds an image.
    /// 新增圖片。
    /// </summary>
    /// <param name="imageBytes">The image bytes. / 圖片位元組。</param>
    /// <param name="xCm">The left position in centimeters. / 左側位置，單位為公分。</param>
    /// <param name="yCm">The top position in centimeters. / 上方位置，單位為公分。</param>
    /// <param name="widthCm">The width in centimeters. / 寬度，單位為公分。</param>
    /// <param name="heightCm">The height in centimeters. / 高度，單位為公分。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
    /// Adds a basic shape.
    /// 新增基本圖形。
    /// </summary>
    /// <param name="shapeType">The shape type. / 圖形類型。</param>
    /// <param name="xCm">The left position in centimeters. / 左側位置，單位為公分。</param>
    /// <param name="yCm">The top position in centimeters. / 上方位置，單位為公分。</param>
    /// <param name="widthCm">The width in centimeters. / 寬度，單位為公分。</param>
    /// <param name="heightCm">The height in centimeters. / 高度，單位為公分。</param>
    /// <param name="configure">The shape configuration delegate. / 圖形設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
    /// Adds a chart placeholder.
    /// 新增圖表預留位置。
    /// </summary>
    /// <param name="xCm">The left position in centimeters. / 左側位置，單位為公分。</param>
    /// <param name="yCm">The top position in centimeters. / 上方位置，單位為公分。</param>
    /// <param name="widthCm">The width in centimeters. / 寬度，單位為公分。</param>
    /// <param name="heightCm">The height in centimeters. / 高度，單位為公分。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
    /// Applies a standard slide layout.
    /// 套用標準投影片版面。
    /// </summary>
    /// <param name="layout">The slide layout. / 投影片版面。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfSlideBuilder WithLayout(OdfPresentationLayout layout)
    {
        _slide.SetLayout(layout);
        return this;
    }

    /// <summary>
    /// Adds a slide emphasis animation.
    /// 新增投影片強調動畫。
    /// </summary>
    /// <param name="shapeId">The target shape identifier. / 目標圖形識別碼。</param>
    /// <param name="effect">The animation effect. / 動畫效果。</param>
    /// <param name="durationSeconds">The duration in seconds. / 持續秒數。</param>
    /// <param name="trigger">The trigger mode. / 觸發方式。</param>
    /// <param name="delaySeconds">The delay in seconds. / 延遲秒數。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
    /// Adds a slide exit animation.
    /// 新增投影片退場動畫。
    /// </summary>
    /// <param name="shapeId">The target shape identifier. / 目標圖形識別碼。</param>
    /// <param name="effect">The animation effect. / 動畫效果。</param>
    /// <param name="trigger">The trigger mode. / 觸發方式。</param>
    /// <param name="delaySeconds">The delay in seconds. / 延遲秒數。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
    /// Adds a title placeholder.
    /// 新增標題預留位置。
    /// </summary>
    /// <param name="xCm">The left position in centimeters. / 左側位置，單位為公分。</param>
    /// <param name="yCm">The top position in centimeters. / 上方位置，單位為公分。</param>
    /// <param name="widthCm">The width in centimeters. / 寬度，單位為公分。</param>
    /// <param name="heightCm">The height in centimeters. / 高度，單位為公分。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
    /// Sets speaker notes text.
    /// 設定講者備忘文字。
    /// </summary>
    /// <param name="notes">The speaker notes content. / 講者備忘內容。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfSlideBuilder WithSpeakerNotes(string notes)
    {
        _slide.SpeakerNotes = notes;
        return this;
    }

    /// <summary>
    /// Sets multi-paragraph speaker notes text.
    /// 設定多段落講者備忘文字。
    /// </summary>
    /// <param name="paragraphs">The speaker notes paragraph collection. / 講者備忘段落集合。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfSlideBuilder WithSpeakerNotes(IEnumerable<string> paragraphs)
    {
        _slide.SetSpeakerNotes(paragraphs);
        return this;
    }

    /// <summary>
    /// Sets the slide transition effect.
    /// 設定投影片切換效果。
    /// </summary>
    /// <param name="type">The transition type. / 切換類型。</param>
    /// <param name="durationPoints">The duration in points. / 持續時間，單位為點。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfSlideBuilder WithTransition(OdfTransitionType type, double durationPoints = 72)
    {
        _slide.SetTransition(type, OdfLength.FromPoints(durationPoints));
        return this;
    }

    /// <summary>
    /// Adds an entrance animation to the specified shape.
    /// 為指定圖形新增進場動畫。
    /// </summary>
    /// <param name="shapeId">The target shape identifier. / 目標圖形識別碼。</param>
    /// <param name="effect">The animation effect. / 動畫效果。</param>
    /// <param name="trigger">The trigger mode. / 觸發方式。</param>
    /// <param name="delaySeconds">The delay in seconds. / 延遲秒數。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
/// Provides a fluent configuration API for presentation shapes.
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
    /// Sets the shape identifier.
    /// 設定圖形識別碼。
    /// </summary>
    /// <param name="id">The shape identifier. / 圖形識別碼。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfPresentationShapeBuilder WithId(string id)
    {
        _shape.Id = id;
        return this;
    }

    /// <summary>
    /// Sets the fill color.
    /// 設定填滿色彩。
    /// </summary>
    /// <param name="color">The color value. / 色彩值。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfPresentationShapeBuilder Fill(string color)
    {
        _shape.FillColor = color;
        return this;
    }

    /// <summary>
    /// Sets the stroke color.
    /// 設定線條色彩。
    /// </summary>
    /// <param name="color">The color value. / 色彩值。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfPresentationShapeBuilder Stroke(string color)
    {
        _shape.StrokeColor = color;
        return this;
    }
}
