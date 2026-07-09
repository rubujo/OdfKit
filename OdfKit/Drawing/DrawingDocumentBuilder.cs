using OdfKit.Presentation;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Text;

namespace OdfKit.Drawing;

/// <summary>
/// Provides a fluent creation API for <see cref="DrawingDocument"/>.
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
    /// Configures the document metadata.
    /// 設定文件中繼資料。
    /// </summary>
    /// <param name="configure">The metadata configuration delegate. / 中繼資料設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public DrawingDocumentBuilder WithMetadata(Action<TextDocumentMetadataBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        configure(new TextDocumentMetadataBuilder(new OdfDocumentMetadata(_document)));
        return this;
    }

    /// <summary>
    /// Sets the design theme used by subsequent drawing pages and shapes.
    /// 設定後續繪圖頁面與圖形使用的設計主題。
    /// </summary>
    /// <param name="theme">The design theme. / 設計主題。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public DrawingDocumentBuilder WithTheme(OdfDesignTheme theme)
    {
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        return this;
    }

    /// <summary>
    /// Sets the style set used by subsequent drawing pages and shapes.
    /// 設定後續繪圖頁面與圖形使用的樣式集合。
    /// </summary>
    /// <param name="styles">The style set. / 樣式集合。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public DrawingDocumentBuilder WithStyles(OdfStyleSet styles)
    {
        ApplyStyleSetToTheme(styles ?? throw new ArgumentNullException(nameof(styles)));
        return this;
    }

    /// <summary>
    /// Sets the style set used by subsequent drawing pages and shapes.
    /// 設定後續繪圖頁面與圖形使用的樣式集合。
    /// </summary>
    /// <param name="configure">The style set configuration delegate. / 樣式集合設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public DrawingDocumentBuilder WithStyles(Action<OdfStyleSet> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        var styles = new OdfStyleSet();
        configure(styles);
        return WithStyles(styles);
    }

    /// <summary>
    /// Sets the layout preset used by subsequent drawing helpers.
    /// 設定後續繪圖 helper 使用的版面 preset。
    /// </summary>
    /// <param name="preset">The layout preset. / 版面 preset。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public DrawingDocumentBuilder WithLayoutPreset(OdfLayoutPreset preset)
    {
        _layoutPreset = preset ?? throw new ArgumentNullException(nameof(preset));
        return this;
    }

    /// <summary>
    /// Adds a drawing page and configures its content.
    /// 新增繪圖頁面並設定其內容。
    /// </summary>
    /// <param name="name">The page name. / 頁面名稱。</param>
    /// <param name="configure">The page configuration delegate. / 頁面設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
    /// Adds a drawing page and configures its content.
    /// 新增繪圖頁面並設定其內容。
    /// </summary>
    /// <param name="configure">The page configuration delegate. / 頁面設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public DrawingDocumentBuilder AddPage(Action<OdfDrawPageBuilder> configure)
    {
        return AddPage($"Page {_pageCount + 1}", configure);
    }

    /// <summary>
    /// Adds a page containing a connected flow diagram.
    /// 新增包含已連接流程圖的頁面。
    /// </summary>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public DrawingDocumentBuilder AddFlow(string pageName, IEnumerable<OdfFlowStepRequest> steps) => AddFlow(pageName, steps, OdfConnectorType.Straight, null);

    /// <summary>
    /// Short overload of AddFlow that accepts pageName, steps, and connectorType; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 pageName、steps 與 connectorType；其餘可選參數使用預設值並轉呼叫最長 AddFlow 多載。
    /// </summary>
    public DrawingDocumentBuilder AddFlow(string pageName, IEnumerable<OdfFlowStepRequest> steps, OdfConnectorType connectorType) => AddFlow(pageName, steps, connectorType, null);

    /// <summary>
    /// Full overload of AddFlow that accepts pageName, steps, connectorType, and layoutOptions.
    /// AddFlow 完整多載：接受 pageName、steps、connectorType 與 layoutOptions。
    /// </summary>
    public DrawingDocumentBuilder AddFlow(
        string pageName,
        IEnumerable<OdfFlowStepRequest> steps,
        OdfConnectorType connectorType,
        OdfFlowLayoutOptions? layoutOptions)
    {
        if (steps is null)
        {
            throw new ArgumentNullException(nameof(steps));
        }

        OdfFlowStepRequest[] snapshot = steps.ToArray();
        return AddPage(pageName, page =>
        {
            for (int i = 0; i < snapshot.Length; i++)
            {
                OdfFlowStepRequest step = snapshot[i];
                if (layoutOptions is null)
                {
                    page.AddFlowStep(step.Id, step.Text, i, step.ShapeType);
                }
                else
                {
                    double x = layoutOptions.StartXCm + (layoutOptions.Horizontal ? i * (layoutOptions.NodeWidthCm + layoutOptions.GapCm) : 0);
                    double y = layoutOptions.StartYCm + (layoutOptions.Horizontal ? 0 : i * (layoutOptions.NodeHeightCm + layoutOptions.GapCm));
                    if (step.ShapeType == OdfShapeType.Ellipse)
                    {
                        page.AddEllipse(x, y, layoutOptions.NodeWidthCm, layoutOptions.NodeHeightCm, shape => shape.WithId(step.Id));
                    }
                    else
                    {
                        page.AddRectangle(x, y, layoutOptions.NodeWidthCm, layoutOptions.NodeHeightCm, shape => shape.WithId(step.Id));
                    }

                    page.AddTextBox(
                        step.Text,
                        x + 0.3,
                        y + 0.25,
                        Math.Max(0.1, layoutOptions.NodeWidthCm - 0.6),
                        Math.Max(0.1, layoutOptions.NodeHeightCm - 0.5));
                }

                if (i > 0)
                {
                    page.AddConnector(snapshot[i - 1].Id, step.Id, connectorType);
                    string? connectorLabelFormat = layoutOptions?.ConnectorLabelFormat;
                    if (layoutOptions is not null && !string.IsNullOrWhiteSpace(connectorLabelFormat))
                    {
                        double labelX = layoutOptions.StartXCm +
                            (layoutOptions.Horizontal
                                ? (i - 0.5) * (layoutOptions.NodeWidthCm + layoutOptions.GapCm)
                                : layoutOptions.NodeWidthCm + 0.4);
                        double labelY = layoutOptions.StartYCm +
                            (layoutOptions.Horizontal
                                ? layoutOptions.NodeHeightCm + 0.2
                                : (i - 0.5) * (layoutOptions.NodeHeightCm + layoutOptions.GapCm));
                        page.AddTextBox(
                            string.Format(
                                System.Globalization.CultureInfo.InvariantCulture,
                                connectorLabelFormat!,
                                snapshot[i - 1].Id,
                                step.Id),
                            labelX,
                            labelY,
                            Math.Max(1, layoutOptions.NodeWidthCm),
                            0.5);
                    }
                }
            }
        });
    }

    /// <summary>
    /// Adds a default-named page containing a connected flow diagram.
    /// 新增包含已連接流程圖的預設命名頁面。
    /// </summary>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public DrawingDocumentBuilder AddFlow(IEnumerable<OdfFlowStepRequest> steps) => AddFlow($"Page {_pageCount + 1}", steps, OdfConnectorType.Straight, null);

    /// <summary>
    /// Short overload of AddFlow that accepts steps and connectorType; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 steps 與 connectorType；其餘可選參數使用預設值並轉呼叫最長 AddFlow 多載。
    /// </summary>
    public DrawingDocumentBuilder AddFlow(IEnumerable<OdfFlowStepRequest> steps, OdfConnectorType connectorType) => AddFlow($"Page {_pageCount + 1}", steps, connectorType, null);

    /// <summary>
    /// Full overload of AddFlow that accepts steps, connectorType, and layoutOptions.
    /// AddFlow 完整多載：接受 steps、connectorType 與 layoutOptions。
    /// </summary>
    public DrawingDocumentBuilder AddFlow(
        IEnumerable<OdfFlowStepRequest> steps,
        OdfConnectorType connectorType,
        OdfFlowLayoutOptions? layoutOptions)
    {
        return AddFlow($"Page {_pageCount + 1}", steps, connectorType, layoutOptions);
    }

    /// <summary>
    /// Builds and returns the drawing document.
    /// 建立並傳回繪圖文件。
    /// </summary>
    /// <returns>The built drawing document. / 建立完成的繪圖文件。</returns>
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
/// Provides a fluent creation API for drawing page content.
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
    /// Short overload of AddRectangle that accepts xCm, yCm, widthCm, and heightCm; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 xCm、yCm、widthCm 與 heightCm；其餘可選參數使用預設值並轉呼叫最長 AddRectangle 多載。
    /// </summary>
    public OdfDrawPageBuilder AddRectangle(double xCm, double yCm, double widthCm, double heightCm) => AddRectangle(xCm, yCm, widthCm, heightCm, null);


    /// <summary>
    /// Adds a rectangle shape.
    /// 新增矩形圖形。
    /// </summary>
    /// <param name="xCm">The left position in centimeters. / 左側位置（公分）。</param>
    /// <param name="yCm">The top position in centimeters. / 上方位置（公分）。</param>
    /// <param name="widthCm">The width in centimeters. / 寬度（公分）。</param>
    /// <param name="heightCm">The height in centimeters. / 高度（公分）。</param>
    /// <param name="configure">The shape configuration delegate. / 圖形設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfDrawPageBuilder AddRectangle(double xCm, double yCm, double widthCm, double heightCm, Action<OdfDrawShapeBuilder>? configure)
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
    /// Short overload of AddEllipse that accepts xCm, yCm, widthCm, and heightCm; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 xCm、yCm、widthCm 與 heightCm；其餘可選參數使用預設值並轉呼叫最長 AddEllipse 多載。
    /// </summary>
    public OdfDrawPageBuilder AddEllipse(double xCm, double yCm, double widthCm, double heightCm) => AddEllipse(xCm, yCm, widthCm, heightCm, null);


    /// <summary>
    /// Adds an ellipse shape.
    /// 新增橢圓圖形。
    /// </summary>
    /// <param name="xCm">The left position in centimeters. / 左側位置（公分）。</param>
    /// <param name="yCm">The top position in centimeters. / 上方位置（公分）。</param>
    /// <param name="widthCm">The width in centimeters. / 寬度（公分）。</param>
    /// <param name="heightCm">The height in centimeters. / 高度（公分）。</param>
    /// <param name="configure">The shape configuration delegate. / 圖形設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfDrawPageBuilder AddEllipse(double xCm, double yCm, double widthCm, double heightCm, Action<OdfDrawShapeBuilder>? configure)
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
    /// Short overload of AddFlowStep that accepts id, text, and index; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 id、text 與 index；其餘可選參數使用預設值並轉呼叫最長 AddFlowStep 多載。
    /// </summary>
    public OdfDrawPageBuilder AddFlowStep(string id, string text, int index) => AddFlowStep(id, text, index, OdfShapeType.Rectangle, null);

    /// <summary>
    /// Short overload of AddFlowStep that accepts id, text, index, and shapeType; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 id、text、index 與 shapeType；其餘可選參數使用預設值並轉呼叫最長 AddFlowStep 多載。
    /// </summary>
    public OdfDrawPageBuilder AddFlowStep(string id, string text, int index, OdfShapeType shapeType) => AddFlowStep(id, text, index, shapeType, null);



    /// <summary>
    /// Adds a flow-diagram node and its text according to the layout preset.
    /// 依版面 preset 新增流程圖節點與文字。
    /// </summary>
    /// <param name="id">The shape identifier. / 圖形識別碼。</param>
    /// <param name="text">The node text. / 節點文字。</param>
    /// <param name="index">The node sequence number. / 節點序號。</param>
    /// <param name="shapeType">The node shape type. / 節點圖形類型。</param>
    /// <param name="configure">The shape configuration delegate. / 圖形設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfDrawPageBuilder AddFlowStep(string id, string text, int index, OdfShapeType shapeType, Action<OdfDrawShapeBuilder>? configure)
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
    /// Adds a text box.
    /// 新增文字方塊。
    /// </summary>
    /// <param name="text">The text content. / 文字內容。</param>
    /// <param name="xCm">The left position in centimeters. / 左側位置（公分）。</param>
    /// <param name="yCm">The top position in centimeters. / 上方位置（公分）。</param>
    /// <param name="widthCm">The width in centimeters. / 寬度（公分）。</param>
    /// <param name="heightCm">The height in centimeters. / 高度（公分）。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
    /// Short overload of AddPath that accepts svgPathData, xCm, yCm, widthCm, and heightCm; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 svgPathData、xCm、yCm、widthCm 與 heightCm；其餘可選參數使用預設值並轉呼叫最長 AddPath 多載。
    /// </summary>
    public OdfDrawPageBuilder AddPath(string svgPathData, double xCm, double yCm, double widthCm, double heightCm) => AddPath(svgPathData, xCm, yCm, widthCm, heightCm, null);


    /// <summary>
    /// Adds an SVG path shape.
    /// 新增 SVG 路徑圖形。
    /// </summary>
    /// <param name="svgPathData">The SVG path data. / SVG path 資料。</param>
    /// <param name="xCm">The left position in centimeters. / 左側位置（公分）。</param>
    /// <param name="yCm">The top position in centimeters. / 上方位置（公分）。</param>
    /// <param name="widthCm">The width in centimeters. / 寬度（公分）。</param>
    /// <param name="heightCm">The height in centimeters. / 高度（公分）。</param>
    /// <param name="configure">The shape configuration delegate. / 圖形設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfDrawPageBuilder AddPath(string svgPathData, double xCm, double yCm, double widthCm, double heightCm, Action<OdfDrawShapeBuilder>? configure)
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
    /// Short overload of AddLine that accepts x1Cm, y1Cm, x2Cm, and y2Cm; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 x1Cm、y1Cm、x2Cm 與 y2Cm；其餘可選參數使用預設值並轉呼叫最長 AddLine 多載。
    /// </summary>
    public OdfDrawPageBuilder AddLine(double x1Cm, double y1Cm, double x2Cm, double y2Cm) => AddLine(x1Cm, y1Cm, x2Cm, y2Cm, null);


    /// <summary>
    /// Adds a line segment.
    /// 新增線段。
    /// </summary>
    /// <param name="x1Cm">The start X position in centimeters. / 起點 X 位置（公分）。</param>
    /// <param name="y1Cm">The start Y position in centimeters. / 起點 Y 位置（公分）。</param>
    /// <param name="x2Cm">The end X position in centimeters. / 終點 X 位置（公分）。</param>
    /// <param name="y2Cm">The end Y position in centimeters. / 終點 Y 位置（公分）。</param>
    /// <param name="configure">The shape configuration delegate. / 圖形設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfDrawPageBuilder AddLine(double x1Cm, double y1Cm, double x2Cm, double y2Cm, Action<OdfDrawShapeBuilder>? configure)
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
    /// Adds a coordinate-based connector.
    /// 新增座標式連接線。
    /// </summary>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfDrawPageBuilder AddConnector(double x1Cm, double y1Cm, double x2Cm, double y2Cm) => AddConnector(x1Cm, y1Cm, x2Cm, y2Cm, OdfConnectorType.Standard, null);

    /// <summary>
    /// Short overload of AddConnector that accepts x1Cm, y1Cm, x2Cm, y2Cm, and connectorType; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 x1Cm、y1Cm、x2Cm、y2Cm 與 connectorType；其餘可選參數使用預設值並轉呼叫最長 AddConnector 多載。
    /// </summary>
    public OdfDrawPageBuilder AddConnector(double x1Cm, double y1Cm, double x2Cm, double y2Cm, OdfConnectorType connectorType) => AddConnector(x1Cm, y1Cm, x2Cm, y2Cm, connectorType, null);

    /// <summary>
    /// Full overload of AddConnector that accepts x1Cm, y1Cm, x2Cm, y2Cm, connectorType, and configure.
    /// AddConnector 完整多載：接受 x1Cm、y1Cm、x2Cm、y2Cm、connectorType 與 configure。
    /// </summary>
    public OdfDrawPageBuilder AddConnector(
        double x1Cm,
        double y1Cm,
        double x2Cm,
        double y2Cm,
        OdfConnectorType connectorType,
        Action<OdfDrawShapeBuilder>? configure)
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
    /// Adds a connector linking two shapes.
    /// 新增連接兩個圖形的連接線。
    /// </summary>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfDrawPageBuilder AddConnector(string startShapeId, string endShapeId) => AddConnector(startShapeId, endShapeId, OdfConnectorType.Standard, null);

    /// <summary>
    /// Short overload of AddConnector that accepts startShapeId, endShapeId, and connectorType; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 startShapeId、endShapeId 與 connectorType；其餘可選參數使用預設值並轉呼叫最長 AddConnector 多載。
    /// </summary>
    public OdfDrawPageBuilder AddConnector(string startShapeId, string endShapeId, OdfConnectorType connectorType) => AddConnector(startShapeId, endShapeId, connectorType, null);

    /// <summary>
    /// Full overload of AddConnector that accepts startShapeId, endShapeId, connectorType, and configure.
    /// AddConnector 完整多載：接受 startShapeId、endShapeId、connectorType 與 configure。
    /// </summary>
    public OdfDrawPageBuilder AddConnector(
        string startShapeId,
        string endShapeId,
        OdfConnectorType connectorType,
        Action<OdfDrawShapeBuilder>? configure)
    {
        OdfShape shape = _page.AddConnector(startShapeId, endShapeId, connectorType);
        ApplyThemedConnectorStyle(shape);
        configure?.Invoke(new OdfDrawShapeBuilder(shape));
        return this;
    }
    /// <summary>
    /// Short overload of AddImage that accepts imageBytes, xCm, yCm, widthCm, and heightCm; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 imageBytes、xCm、yCm、widthCm 與 heightCm；其餘可選參數使用預設值並轉呼叫最長 AddImage 多載。
    /// </summary>
    public OdfDrawPageBuilder AddImage(byte[] imageBytes, double xCm, double yCm, double widthCm, double heightCm) => AddImage(imageBytes, xCm, yCm, widthCm, heightCm, null);


    /// <summary>
    /// Adds an image.
    /// 新增圖片。
    /// </summary>
    /// <param name="imageBytes">The image byte array. / 圖片位元組。</param>
    /// <param name="xCm">The left position in centimeters. / 左側位置（公分）。</param>
    /// <param name="yCm">The top position in centimeters. / 上方位置（公分）。</param>
    /// <param name="widthCm">The width in centimeters. / 寬度（公分）。</param>
    /// <param name="heightCm">The height in centimeters. / 高度（公分）。</param>
    /// <param name="configure">The shape configuration delegate. / 圖形設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfDrawPageBuilder AddImage(byte[] imageBytes, double xCm, double yCm, double widthCm, double heightCm, Action<OdfDrawShapeBuilder>? configure)
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
    /// Adds a shape group.
    /// 新增圖形群組。
    /// </summary>
    /// <param name="name">The group name. / 群組名稱。</param>
    /// <param name="configure">The group content configuration delegate. / 群組內容設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
    /// Short overload of AddLayer that accepts name; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name；其餘可選參數使用預設值並轉呼叫最長 AddLayer 多載。
    /// </summary>
    public OdfDrawPageBuilder AddLayer(string name) => AddLayer(name, false, "screen");

    /// <summary>
    /// Short overload of AddLayer that accepts name and isProtected; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name 與 isProtected；其餘可選參數使用預設值並轉呼叫最長 AddLayer 多載。
    /// </summary>
    public OdfDrawPageBuilder AddLayer(string name, bool isProtected) => AddLayer(name, isProtected, "screen");


    /// <summary>
    /// Adds a page layer definition.
    /// 新增頁面圖層定義。
    /// </summary>
    /// <param name="name">The layer name. / 圖層名稱。</param>
    /// <param name="isProtected">Whether the layer is read-only. / 圖層是否唯讀。</param>
    /// <param name="display">The display mode. / 顯示模式。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfDrawPageBuilder AddLayer(string name, bool isProtected, string? display)
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
        OdfNode drawingRoot = _page.Document.GetDrawingNode();
        foreach (OdfNode child in drawingRoot.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "layer-set" &&
                child.NamespaceUri == OdfNamespaces.Draw)
            {
                return child;
            }
        }

        OdfNode layerSet = OdfNodeFactory.CreateElement("layer-set", OdfNamespaces.Draw, "draw");
        drawingRoot.InsertBefore(layerSet, _page.Node);
        return layerSet;
    }

    private static string ToConnectorTypeValue(OdfConnectorType connectorType) => connectorType switch
    {
        OdfConnectorType.Lines => "lines",
        OdfConnectorType.Straight => "line",
        OdfConnectorType.Curve => "curve",
        _ => "standard"
    };
}

/// <summary>
/// Provides a fluent configuration API for drawing shapes.
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
    /// Sets the shape identifier.
    /// 設定圖形識別碼。
    /// </summary>
    /// <param name="id">The shape identifier. / 圖形識別碼。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfDrawShapeBuilder WithId(string id)
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
    public OdfDrawShapeBuilder Fill(string color)
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
    public OdfDrawShapeBuilder Stroke(string color)
    {
        _shape.StrokeColor = color;
        return this;
    }

    /// <summary>
    /// Assigns the layer the shape belongs to.
    /// 指派圖形所屬圖層。
    /// </summary>
    /// <param name="layerName">The layer name. / 圖層名稱。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfDrawShapeBuilder OnLayer(string layerName)
    {
        _shape.Node.SetAttribute("layer", OdfNamespaces.Draw, layerName, "draw");
        return this;
    }
}

/// <summary>
/// Provides a fluent creation API for drawing groups.
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
    /// Short overload of AddRectangle that accepts xCm, yCm, widthCm, and heightCm; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 xCm、yCm、widthCm 與 heightCm；其餘可選參數使用預設值並轉呼叫最長 AddRectangle 多載。
    /// </summary>
    public OdfDrawGroupBuilder AddRectangle(double xCm, double yCm, double widthCm, double heightCm) => AddRectangle(xCm, yCm, widthCm, heightCm, null);


    /// <summary>
    /// Adds a rectangle within the group.
    /// 新增群組內矩形。
    /// </summary>
    /// <param name="xCm">The left position in centimeters. / 左側位置（公分）。</param>
    /// <param name="yCm">The top position in centimeters. / 上方位置（公分）。</param>
    /// <param name="widthCm">The width in centimeters. / 寬度（公分）。</param>
    /// <param name="heightCm">The height in centimeters. / 高度（公分）。</param>
    /// <param name="configure">The shape configuration delegate. / 圖形設定委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfDrawGroupBuilder AddRectangle(double xCm, double yCm, double widthCm, double heightCm, Action<OdfDrawShapeBuilder>? configure)
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
    /// Adds a text box within the group.
    /// 新增群組內文字方塊。
    /// </summary>
    /// <param name="text">The text content. / 文字內容。</param>
    /// <param name="xCm">The left position in centimeters. / 左側位置（公分）。</param>
    /// <param name="yCm">The top position in centimeters. / 上方位置（公分）。</param>
    /// <param name="widthCm">The width in centimeters. / 寬度（公分）。</param>
    /// <param name="heightCm">The height in centimeters. / 高度（公分）。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
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
