using OdfKit.Presentation;
using OdfKit.Styles;
using OdfKit.Text;

namespace OdfKit.Drawing;

/// <summary>
/// 提供 <see cref="DrawingDocument"/> 的 Fluent 建立 API。
/// </summary>
public sealed class DrawingDocumentBuilder
{
    private readonly DrawingDocument _document;
    private int _pageCount;

    internal DrawingDocumentBuilder(DrawingDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 設定文件中繼資料。
    /// </summary>
    /// <param name="configure">中繼資料設定委派。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public DrawingDocumentBuilder WithMetadata(Action<TextDocumentMetadataBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        configure(new TextDocumentMetadataBuilder(new OdfDocumentMetadata(_document)));
        return this;
    }

    /// <summary>
    /// 新增繪圖頁面並設定其內容。
    /// </summary>
    /// <param name="name">頁面名稱。</param>
    /// <param name="configure">頁面設定委派。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public DrawingDocumentBuilder AddPage(string name, Action<OdfDrawPageBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        OdfDrawPage page = _document.AddPage(name);
        configure(new OdfDrawPageBuilder(page));
        _pageCount++;
        return this;
    }

    /// <summary>
    /// 新增繪圖頁面並設定其內容。
    /// </summary>
    /// <param name="configure">頁面設定委派。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public DrawingDocumentBuilder AddPage(Action<OdfDrawPageBuilder> configure)
    {
        return AddPage($"Page {_pageCount + 1}", configure);
    }

    /// <summary>
    /// 建立並傳回繪圖文件。
    /// </summary>
    /// <returns>建立完成的繪圖文件。</returns>
    public DrawingDocument Build()
    {
        return _document;
    }
}

/// <summary>
/// 提供繪圖頁面內容的 Fluent 建立 API。
/// </summary>
public sealed class OdfDrawPageBuilder
{
    private readonly OdfDrawPage _page;

    internal OdfDrawPageBuilder(OdfDrawPage page)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
    }

    /// <summary>
    /// 新增矩形圖形。
    /// </summary>
    /// <param name="xCm">左側位置（公分）。</param>
    /// <param name="yCm">上方位置（公分）。</param>
    /// <param name="widthCm">寬度（公分）。</param>
    /// <param name="heightCm">高度（公分）。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public OdfDrawPageBuilder AddRectangle(double xCm, double yCm, double widthCm, double heightCm)
    {
        _page.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(xCm),
            OdfLength.FromCentimeters(yCm),
            OdfLength.FromCentimeters(widthCm),
            OdfLength.FromCentimeters(heightCm));
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
    public OdfDrawPageBuilder AddTextBox(
        string text,
        double xCm,
        double yCm,
        double widthCm,
        double heightCm)
    {
        _page.AddTextBox(
            OdfLength.FromCentimeters(xCm),
            OdfLength.FromCentimeters(yCm),
            OdfLength.FromCentimeters(widthCm),
            OdfLength.FromCentimeters(heightCm),
            text);
        return this;
    }

    /// <summary>
    /// 新增 SVG 路徑圖形。
    /// </summary>
    /// <param name="svgPathData">SVG path 資料。</param>
    /// <param name="xCm">左側位置（公分）。</param>
    /// <param name="yCm">上方位置（公分）。</param>
    /// <param name="widthCm">寬度（公分）。</param>
    /// <param name="heightCm">高度（公分）。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public OdfDrawPageBuilder AddPath(
        string svgPathData,
        double xCm,
        double yCm,
        double widthCm,
        double heightCm)
    {
        _page.AddPath(
            svgPathData,
            OdfLength.FromCentimeters(xCm),
            OdfLength.FromCentimeters(yCm),
            OdfLength.FromCentimeters(widthCm),
            OdfLength.FromCentimeters(heightCm));
        return this;
    }
}
