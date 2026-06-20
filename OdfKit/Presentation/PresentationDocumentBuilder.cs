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
    private int _slideCount;

    internal PresentationDocumentBuilder(PresentationDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 設定文件中繼資料。
    /// </summary>
    /// <param name="configure">中繼資料設定委派。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public PresentationDocumentBuilder WithMetadata(Action<TextDocumentMetadataBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        configure(new TextDocumentMetadataBuilder(new OdfDocumentMetadata(_document)));
        return this;
    }

    /// <summary>
    /// 新增投影片並設定其內容。
    /// </summary>
    /// <param name="name">投影片名稱。</param>
    /// <param name="configure">投影片設定委派。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public PresentationDocumentBuilder AddSlide(string name, Action<OdfSlideBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        OdfSlide slide = _document.AddSlide(name);
        configure(new OdfSlideBuilder(slide));
        _slideCount++;
        return this;
    }

    /// <summary>
    /// 新增投影片並設定其內容。
    /// </summary>
    /// <param name="configure">投影片設定委派。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public PresentationDocumentBuilder AddSlide(Action<OdfSlideBuilder> configure)
    {
        return AddSlide($"Slide {_slideCount + 1}", configure);
    }

    /// <summary>
    /// 建立並傳回簡報文件。
    /// </summary>
    /// <returns>建立完成的簡報文件。</returns>
    public PresentationDocument Build()
    {
        return _document;
    }
}

/// <summary>
/// 提供投影片內容的 Fluent 建立 API。
/// </summary>
public sealed class OdfSlideBuilder
{
    private readonly OdfSlide _slide;

    internal OdfSlideBuilder(OdfSlide slide)
    {
        _slide = slide ?? throw new ArgumentNullException(nameof(slide));
    }

    /// <summary>
    /// 新增標題文字方塊。
    /// </summary>
    /// <param name="text">標題文字。</param>
    /// <param name="xCm">左側位置（公分）。</param>
    /// <param name="yCm">上方位置（公分）。</param>
    /// <param name="widthCm">寬度（公分）。</param>
    /// <param name="heightCm">高度（公分）。</param>
    /// <returns>目前 builder 執行個體。</returns>
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
    /// <param name="text">文字內容。</param>
    /// <param name="xCm">左側位置（公分）。</param>
    /// <param name="yCm">上方位置（公分）。</param>
    /// <param name="widthCm">寬度（公分）。</param>
    /// <param name="heightCm">高度（公分）。</param>
    /// <returns>目前 builder 執行個體。</returns>
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
    /// <param name="paragraphs">段落文字集合。</param>
    /// <param name="xCm">左側位置（公分）。</param>
    /// <param name="yCm">上方位置（公分）。</param>
    /// <param name="widthCm">寬度（公分）。</param>
    /// <param name="heightCm">高度（公分）。</param>
    /// <returns>目前 builder 執行個體。</returns>
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
    /// 新增標題預留位置。
    /// </summary>
    /// <param name="xCm">左側位置（公分）。</param>
    /// <param name="yCm">上方位置（公分）。</param>
    /// <param name="widthCm">寬度（公分）。</param>
    /// <param name="heightCm">高度（公分）。</param>
    /// <returns>目前 builder 執行個體。</returns>
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
    /// <param name="notes">講者備忘內容。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public OdfSlideBuilder WithSpeakerNotes(string notes)
    {
        _slide.SpeakerNotes = notes;
        return this;
    }

    /// <summary>
    /// 設定多段落講者備忘文字。
    /// </summary>
    /// <param name="paragraphs">講者備忘段落集合。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public OdfSlideBuilder WithSpeakerNotes(IEnumerable<string> paragraphs)
    {
        _slide.SetSpeakerNotes(paragraphs);
        return this;
    }

    /// <summary>
    /// 設定投影片切換效果。
    /// </summary>
    /// <param name="type">切換類型。</param>
    /// <param name="durationPoints">持續時間（點）。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public OdfSlideBuilder WithTransition(OdfTransitionType type, double durationPoints = 72)
    {
        _slide.SetTransition(type, OdfLength.FromPoints(durationPoints));
        return this;
    }

    /// <summary>
    /// 為指定圖形新增進場動畫。
    /// </summary>
    /// <param name="shapeId">目標圖形識別碼。</param>
    /// <param name="effect">動畫效果。</param>
    /// <param name="trigger">觸發方式。</param>
    /// <param name="delaySeconds">延遲秒數。</param>
    /// <returns>目前 builder 執行個體。</returns>
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
