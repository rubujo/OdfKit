using OdfKit.Presentation;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Text;

namespace OdfKit.Drawing;

/// <summary>
/// 提供 <see cref="DrawingDocument"/> 的 Fluent 建立 API。
/// </summary>
public sealed class DrawingDocumentBuilder
{
    private readonly DrawingDocument _document;
    private OdfDesignTheme _theme = new();
    private OdfLayoutPreset _layoutPreset = OdfLayoutPreset.FlowDiagram;
    private int _pageCount;

    internal DrawingDocumentBuilder(DrawingDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 設定文件中繼資料。
    /// </summary>
    /// <param name="configure">中繼資料設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public DrawingDocumentBuilder WithMetadata(Action<TextDocumentMetadataBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        configure(new TextDocumentMetadataBuilder(new OdfDocumentMetadata(_document)));
        return this;
    }

    /// <summary>
    /// 設定後續繪圖頁面與圖形使用的設計主題。
    /// </summary>
    /// <param name="theme">設計主題</param>
    /// <returns>目前 builder 執行個體</returns>
    public DrawingDocumentBuilder WithTheme(OdfDesignTheme theme)
    {
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        return this;
    }

    /// <summary>
    /// 設定後續繪圖頁面與圖形使用的樣式集合。
    /// </summary>
    /// <param name="styles">樣式集合</param>
    /// <returns>目前 builder 執行個體</returns>
    public DrawingDocumentBuilder WithStyles(OdfStyleSet styles)
    {
        ApplyStyleSetToTheme(styles ?? throw new ArgumentNullException(nameof(styles)));
        return this;
    }

    /// <summary>
    /// 設定後續繪圖頁面與圖形使用的樣式集合。
    /// </summary>
    /// <param name="configure">樣式集合設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public DrawingDocumentBuilder WithStyles(Action<OdfStyleSet> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        var styles = new OdfStyleSet();
        configure(styles);
        return WithStyles(styles);
    }

    /// <summary>
    /// 設定後續繪圖 helper 使用的版面 preset。
    /// </summary>
    /// <param name="preset">版面 preset</param>
    /// <returns>目前 builder 執行個體</returns>
    public DrawingDocumentBuilder WithLayoutPreset(OdfLayoutPreset preset)
    {
        _layoutPreset = preset ?? throw new ArgumentNullException(nameof(preset));
        return this;
    }

    /// <summary>
    /// 新增繪圖頁面並設定其內容。
    /// </summary>
    /// <param name="name">頁面名稱</param>
    /// <param name="configure">頁面設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public DrawingDocumentBuilder AddPage(string name, Action<OdfDrawPageBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        OdfDrawPage page = _document.AddPage(name);
        configure(new OdfDrawPageBuilder(page, _theme, _layoutPreset));
        _pageCount++;
        return this;
    }

    /// <summary>
    /// 新增繪圖頁面並設定其內容。
    /// </summary>
    /// <param name="configure">頁面設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public DrawingDocumentBuilder AddPage(Action<OdfDrawPageBuilder> configure)
    {
        return AddPage($"Page {_pageCount + 1}", configure);
    }

    /// <summary>
    /// 建立並傳回繪圖文件。
    /// </summary>
    /// <returns>建立完成的繪圖文件</returns>
    public DrawingDocument Build()
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
/// 提供繪圖頁面內容的 Fluent 建立 API。
/// </summary>
public sealed class OdfDrawPageBuilder
{
    private readonly OdfDrawPage _page;
    private readonly OdfDesignTheme _theme;
    private readonly OdfLayoutPreset _layoutPreset;
    private int _shapeCount;

    internal OdfDrawPageBuilder(OdfDrawPage page, OdfDesignTheme theme, OdfLayoutPreset layoutPreset)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        _layoutPreset = layoutPreset ?? throw new ArgumentNullException(nameof(layoutPreset));
    }

    /// <summary>
    /// 新增矩形圖形。
    /// </summary>
    /// <param name="xCm">左側位置（公分）</param>
    /// <param name="yCm">上方位置（公分）</param>
    /// <param name="widthCm">寬度（公分）</param>
    /// <param name="heightCm">高度（公分）</param>
    /// <param name="configure">圖形設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfDrawPageBuilder AddRectangle(
        double xCm,
        double yCm,
        double widthCm,
        double heightCm,
        Action<OdfDrawShapeBuilder>? configure = null)
    {
        OdfShape shape = _page.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(xCm),
            OdfLength.FromCentimeters(yCm),
            OdfLength.FromCentimeters(widthCm),
            OdfLength.FromCentimeters(heightCm));
        ApplyThemedShapeStyle(shape);
        configure?.Invoke(new OdfDrawShapeBuilder(shape));
        return this;
    }

    /// <summary>
    /// 新增橢圓圖形。
    /// </summary>
    /// <param name="xCm">左側位置（公分）</param>
    /// <param name="yCm">上方位置（公分）</param>
    /// <param name="widthCm">寬度（公分）</param>
    /// <param name="heightCm">高度（公分）</param>
    /// <param name="configure">圖形設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfDrawPageBuilder AddEllipse(
        double xCm,
        double yCm,
        double widthCm,
        double heightCm,
        Action<OdfDrawShapeBuilder>? configure = null)
    {
        OdfShape shape = _page.AddShape(
            OdfShapeType.Ellipse,
            OdfLength.FromCentimeters(xCm),
            OdfLength.FromCentimeters(yCm),
            OdfLength.FromCentimeters(widthCm),
            OdfLength.FromCentimeters(heightCm));
        ApplyThemedShapeStyle(shape);
        configure?.Invoke(new OdfDrawShapeBuilder(shape));
        return this;
    }

    /// <summary>
    /// 依版面 preset 新增流程圖節點與文字。
    /// </summary>
    /// <param name="id">圖形識別碼</param>
    /// <param name="text">節點文字</param>
    /// <param name="index">節點序號</param>
    /// <param name="shapeType">節點圖形類型</param>
    /// <param name="configure">圖形設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfDrawPageBuilder AddFlowStep(
        string id,
        string text,
        int index,
        OdfShapeType shapeType = OdfShapeType.Rectangle,
        Action<OdfDrawShapeBuilder>? configure = null)
    {
        OdfLayoutBounds bounds = _layoutPreset.GetFlowNodeBounds(index);
        OdfShape shape = _page.AddShape(
            shapeType,
            OdfLength.FromCentimeters(bounds.XCm),
            OdfLength.FromCentimeters(bounds.YCm),
            OdfLength.FromCentimeters(bounds.WidthCm),
            OdfLength.FromCentimeters(bounds.HeightCm));
        ApplyThemedShapeStyle(shape);
        var builder = new OdfDrawShapeBuilder(shape);
        builder.WithId(id);
        configure?.Invoke(builder);

        AddTextBox(
            text,
            bounds.XCm + 0.3,
            bounds.YCm + 0.25,
            Math.Max(0.1, bounds.WidthCm - 0.6),
            Math.Max(0.1, bounds.HeightCm - 0.5));
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
    /// <param name="svgPathData">SVG path 資料</param>
    /// <param name="xCm">左側位置（公分）</param>
    /// <param name="yCm">上方位置（公分）</param>
    /// <param name="widthCm">寬度（公分）</param>
    /// <param name="heightCm">高度（公分）</param>
    /// <param name="configure">圖形設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfDrawPageBuilder AddPath(
        string svgPathData,
        double xCm,
        double yCm,
        double widthCm,
        double heightCm,
        Action<OdfDrawShapeBuilder>? configure = null)
    {
        OdfShape shape = _page.AddPath(
            svgPathData,
            OdfLength.FromCentimeters(xCm),
            OdfLength.FromCentimeters(yCm),
            OdfLength.FromCentimeters(widthCm),
            OdfLength.FromCentimeters(heightCm));
        ApplyThemedShapeStyle(shape);
        configure?.Invoke(new OdfDrawShapeBuilder(shape));
        return this;
    }

    /// <summary>
    /// 新增線段。
    /// </summary>
    /// <param name="x1Cm">起點 X 位置（公分）</param>
    /// <param name="y1Cm">起點 Y 位置（公分）</param>
    /// <param name="x2Cm">終點 X 位置（公分）</param>
    /// <param name="y2Cm">終點 Y 位置（公分）</param>
    /// <param name="configure">圖形設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfDrawPageBuilder AddLine(
        double x1Cm,
        double y1Cm,
        double x2Cm,
        double y2Cm,
        Action<OdfDrawShapeBuilder>? configure = null)
    {
        OdfShape shape = _page.AddLine(
            OdfLength.FromCentimeters(x1Cm),
            OdfLength.FromCentimeters(y1Cm),
            OdfLength.FromCentimeters(x2Cm),
            OdfLength.FromCentimeters(y2Cm));
        ApplyThemedConnectorStyle(shape);
        configure?.Invoke(new OdfDrawShapeBuilder(shape));
        return this;
    }

    /// <summary>
    /// 新增座標式連接線。
    /// </summary>
    /// <param name="x1Cm">起點 X 位置（公分）</param>
    /// <param name="y1Cm">起點 Y 位置（公分）</param>
    /// <param name="x2Cm">終點 X 位置（公分）</param>
    /// <param name="y2Cm">終點 Y 位置（公分）</param>
    /// <param name="connectorType">連接線幾何類型</param>
    /// <param name="configure">圖形設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfDrawPageBuilder AddConnector(
        double x1Cm,
        double y1Cm,
        double x2Cm,
        double y2Cm,
        OdfConnectorType connectorType = OdfConnectorType.Standard,
        Action<OdfDrawShapeBuilder>? configure = null)
    {
        OdfShape shape = _page.AddConnector(
            OdfLength.FromCentimeters(x1Cm),
            OdfLength.FromCentimeters(y1Cm),
            OdfLength.FromCentimeters(x2Cm),
            OdfLength.FromCentimeters(y2Cm));
        shape.Node.SetAttribute("type", OdfNamespaces.Draw, ToConnectorTypeValue(connectorType), "draw");
        ApplyThemedConnectorStyle(shape);
        configure?.Invoke(new OdfDrawShapeBuilder(shape));
        return this;
    }

    /// <summary>
    /// 新增連接兩個圖形的連接線。
    /// </summary>
    /// <param name="startShapeId">起點圖形識別碼</param>
    /// <param name="endShapeId">終點圖形識別碼</param>
    /// <param name="connectorType">連接線幾何類型</param>
    /// <param name="configure">圖形設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfDrawPageBuilder AddConnector(
        string startShapeId,
        string endShapeId,
        OdfConnectorType connectorType = OdfConnectorType.Standard,
        Action<OdfDrawShapeBuilder>? configure = null)
    {
        OdfShape shape = _page.AddConnector(startShapeId, endShapeId, connectorType);
        ApplyThemedConnectorStyle(shape);
        configure?.Invoke(new OdfDrawShapeBuilder(shape));
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
    /// <param name="configure">圖形設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfDrawPageBuilder AddImage(
        byte[] imageBytes,
        double xCm,
        double yCm,
        double widthCm,
        double heightCm,
        Action<OdfDrawShapeBuilder>? configure = null)
    {
        OdfPicture picture = _page.AddPicture(
            imageBytes,
            OdfLength.FromCentimeters(xCm),
            OdfLength.FromCentimeters(yCm),
            OdfLength.FromCentimeters(widthCm),
            OdfLength.FromCentimeters(heightCm));
        configure?.Invoke(new OdfDrawShapeBuilder(picture));
        return this;
    }

    /// <summary>
    /// 新增圖形群組。
    /// </summary>
    /// <param name="name">群組名稱</param>
    /// <param name="configure">群組內容設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfDrawPageBuilder AddGroup(string name, Action<OdfDrawGroupBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        OdfDrawGroup group = _page.AddGroup(name);
        configure(new OdfDrawGroupBuilder(group, _theme));
        return this;
    }

    private void ApplyThemedShapeStyle(OdfShape shape)
    {
        shape.FillColor = _theme.GetAccentFillColor(_shapeCount);
        shape.StrokeColor = _theme.StrokeColor;
        _shapeCount++;
    }

    private void ApplyThemedConnectorStyle(OdfShape shape)
    {
        shape.StrokeColor = _theme.ConnectorColor;
    }

    /// <summary>
    /// 新增頁面圖層定義。
    /// </summary>
    /// <param name="name">圖層名稱</param>
    /// <param name="isProtected">圖層是否唯讀</param>
    /// <param name="display">顯示模式</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfDrawPageBuilder AddLayer(string name, bool isProtected = false, string? display = "screen")
    {
        OdfNode layerSet = GetOrCreateLayerSet();
        var layerNode = OdfNodeFactory.CreateElement("layer", OdfNamespaces.Draw, "draw");
        layerNode.SetAttribute("name", OdfNamespaces.Draw, name, "draw");
        if (isProtected)
        {
            layerNode.SetAttribute("protected", OdfNamespaces.Draw, "true", "draw");
        }

        if (!string.IsNullOrEmpty(display))
        {
            layerNode.SetAttribute("display", OdfNamespaces.Draw, display!, "draw");
        }

        layerSet.AppendChild(layerNode);
        return this;
    }

    private OdfNode GetOrCreateLayerSet()
    {
        foreach (OdfNode child in _page.Node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "layer-set" &&
                child.NamespaceUri == OdfNamespaces.Draw)
            {
                return child;
            }
        }

        OdfNode layerSet = OdfNodeFactory.CreateElement("layer-set", OdfNamespaces.Draw, "draw");
        _page.Node.AppendChild(layerSet);
        return layerSet;
    }

    private static string ToConnectorTypeValue(OdfConnectorType connectorType) => connectorType switch
    {
        OdfConnectorType.Lines => "lines",
        OdfConnectorType.Straight => "straight",
        OdfConnectorType.Curve => "curve",
        _ => "standard"
    };
}

/// <summary>
/// 提供繪圖圖形的 Fluent 設定 API。
/// </summary>
public sealed class OdfDrawShapeBuilder
{
    private readonly OdfShape _shape;

    internal OdfDrawShapeBuilder(OdfShape shape)
    {
        _shape = shape ?? throw new ArgumentNullException(nameof(shape));
    }

    /// <summary>
    /// 設定圖形識別碼。
    /// </summary>
    /// <param name="id">圖形識別碼</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfDrawShapeBuilder WithId(string id)
    {
        _shape.Id = id;
        return this;
    }

    /// <summary>
    /// 設定填滿色彩。
    /// </summary>
    /// <param name="color">色彩值</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfDrawShapeBuilder Fill(string color)
    {
        _shape.FillColor = color;
        return this;
    }

    /// <summary>
    /// 設定線條色彩。
    /// </summary>
    /// <param name="color">色彩值</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfDrawShapeBuilder Stroke(string color)
    {
        _shape.StrokeColor = color;
        return this;
    }

    /// <summary>
    /// 指派圖形所屬圖層。
    /// </summary>
    /// <param name="layerName">圖層名稱</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfDrawShapeBuilder OnLayer(string layerName)
    {
        _shape.Node.SetAttribute("layer", OdfNamespaces.Draw, layerName, "draw");
        return this;
    }
}

/// <summary>
/// 提供繪圖群組的 Fluent 建立 API。
/// </summary>
public sealed class OdfDrawGroupBuilder
{
    private readonly OdfDrawGroup _group;
    private readonly OdfDesignTheme _theme;
    private int _shapeCount;

    internal OdfDrawGroupBuilder(OdfDrawGroup group, OdfDesignTheme theme)
    {
        _group = group ?? throw new ArgumentNullException(nameof(group));
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
    }

    /// <summary>
    /// 新增群組內矩形。
    /// </summary>
    /// <param name="xCm">左側位置（公分）</param>
    /// <param name="yCm">上方位置（公分）</param>
    /// <param name="widthCm">寬度（公分）</param>
    /// <param name="heightCm">高度（公分）</param>
    /// <param name="configure">圖形設定委派</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfDrawGroupBuilder AddRectangle(
        double xCm,
        double yCm,
        double widthCm,
        double heightCm,
        Action<OdfDrawShapeBuilder>? configure = null)
    {
        OdfShape shape = _group.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(xCm),
            OdfLength.FromCentimeters(yCm),
            OdfLength.FromCentimeters(widthCm),
            OdfLength.FromCentimeters(heightCm));
        ApplyThemedShapeStyle(shape);
        configure?.Invoke(new OdfDrawShapeBuilder(shape));
        return this;
    }

    /// <summary>
    /// 新增群組內文字方塊。
    /// </summary>
    /// <param name="text">文字內容</param>
    /// <param name="xCm">左側位置（公分）</param>
    /// <param name="yCm">上方位置（公分）</param>
    /// <param name="widthCm">寬度（公分）</param>
    /// <param name="heightCm">高度（公分）</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfDrawGroupBuilder AddTextBox(
        string text,
        double xCm,
        double yCm,
        double widthCm,
        double heightCm)
    {
        _group.AddTextBox(
            OdfLength.FromCentimeters(xCm),
            OdfLength.FromCentimeters(yCm),
            OdfLength.FromCentimeters(widthCm),
            OdfLength.FromCentimeters(heightCm),
            text);
        return this;
    }

    private void ApplyThemedShapeStyle(OdfShape shape)
    {
        shape.FillColor = _theme.GetAccentFillColor(_shapeCount);
        shape.StrokeColor = _theme.StrokeColor;
        _shapeCount++;
    }
}
