using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

using OdfKit.Compliance;
namespace OdfKit.Presentation;

/// <summary>
/// Represents the base class for shapes within a slide.
/// 表示投影片內圖形的基底類別。
/// </summary>
/// <param name="node">The underlying <see cref="OdfNode"/> instance. / 底層的 <see cref="OdfNode"/> 執行個體。</param>
/// <param name="doc">The owning document instance. / 所屬的文件執行個體。</param>
/// <param name="slide">The owning slide instance, or <c>null</c> when the shape does not belong to a presentation slide. / 所屬的投影片執行個體，若不屬於簡報投影片則為 <c>null</c>。</param>
public partial class OdfShape(OdfNode node, OdfDocument doc, OdfSlide? slide)
{
    /// <summary>
    /// 取得底層的 ODF 節點。
    /// </summary>
    internal OdfNode Node { get; } = node;

    /// <summary>
    /// Gets the owning slide instance.
    /// 取得所屬的投影片執行個體。
    /// </summary>
    public OdfSlide? Slide { get; } = slide;

    /// <summary>
    /// Gets the owning ODF document.
    /// 取得所屬的 ODF 文件。
    /// </summary>
    public OdfDocument Document { get; } = doc;

    /// <summary>
    /// Gets the local name of the shape node.
    /// 取得圖形節點的區域名稱。
    /// </summary>
    public string LocalName => Node.LocalName;

    /// <summary>
    /// Gets whether this shape is marked as decorative, including LibreOffice <c>loext:decorative</c> compatibility reads.
    /// 取得此圖形是否標記為裝飾性（含 LibreOffice <c>loext:decorative</c> 相容讀取）。
    /// </summary>
    public bool IsDecorative => OdfLoExtInteropEngine.IsDecorative(Node);

    /// <summary>
    /// Marks this shape as decorative so assistive technologies should skip it.
    /// 將此圖形標記為裝飾性，輔助技術應略過此物件。
    /// </summary>
    /// <param name="decorative">Whether to mark the shape as decorative. / 是否標記為裝飾性。</param>
    /// <returns>The current shape instance. / 目前圖形執行個體。</returns>
    public OdfShape MarkAsDecorative(bool decorative = true)
    {
        if (decorative)
        {
            Node.SetAttribute("decorative", OdfNamespaces.Draw, "true", "draw");
        }
        else
        {
            Node.RemoveAttribute("decorative", OdfNamespaces.Draw);
        }

        Node.RemoveAttribute("decorative", OdfNamespaces.LoExt);
        return this;
    }

    /// <summary>
    /// Creates an embedded table inside this shape frame.
    /// 在此圖形框架內建立嵌入表格。
    /// </summary>
    /// <param name="rows">The row count. / 列數。</param>
    /// <param name="columns">The column count. / 欄數。</param>
    /// <returns>The newly created embedded table. / 新建立的嵌入表格。</returns>
    public OdfEmbeddedTable AddEmbeddedTable(int rows, int columns)
    {
        if (rows < 1)
            throw new ArgumentOutOfRangeException(nameof(rows));
        if (columns < 1)
            throw new ArgumentOutOfRangeException(nameof(columns));

        var table = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");
        for (int row = 0; row < rows; row++)
        {
            var rowNode = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");
            for (int column = 0; column < columns; column++)
            {
                var cell = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");
                var paragraph = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
                cell.AppendChild(paragraph);
                rowNode.AppendChild(cell);
            }

            table.AppendChild(rowNode);
        }

        Node.AppendChild(table);
        return new OdfEmbeddedTable(table, Document);
    }

    /// <summary>
    /// Gets or sets the shape identifier.
    /// 取得或設定圖形的識別碼。
    /// </summary>
    public string Id
    {
        get => Node.GetAttribute("id", OdfNamespaces.Draw) ?? Node.GetAttribute("id", OdfNamespaces.Xml) ?? string.Empty;
        set
        {
            Node.SetAttribute("id", OdfNamespaces.Draw, value, "draw");
            Node.SetAttribute("id", OdfNamespaces.Xml, value, "xml");
        }
    }

    /// <summary>
    /// Gets or sets the shape fill color.
    /// 取得或設定圖形的填滿色彩。
    /// </summary>
    public string? FillColor
    {
        get => Document.StyleEngine.GetStyleProperty(Node.GetAttribute("style-name", OdfNamespaces.Draw) ?? string.Empty, "fill-color", OdfNamespaces.Draw, "graphic");
        set
        {
            Document.StyleEngine.SetLocalStyleProperty(Node, "graphic", "graphic-properties", "fill", OdfNamespaces.Draw, "solid", "draw");
            Document.StyleEngine.SetLocalStyleProperty(Node, "graphic", "graphic-properties", "fill-color", OdfNamespaces.Draw, value ?? string.Empty, "draw");
        }
    }

    /// <summary>
    /// Gets or sets the shape stroke color.
    /// 取得或設定圖形的邊框色彩。
    /// </summary>
    public string? StrokeColor
    {
        get => Document.StyleEngine.GetStyleProperty(Node.GetAttribute("style-name", OdfNamespaces.Draw) ?? string.Empty, "stroke-color", OdfNamespaces.Svg, "graphic");
        set
        {
            Document.StyleEngine.SetLocalStyleProperty(Node, "graphic", "graphic-properties", "stroke", OdfNamespaces.Draw, "solid", "draw");
            Document.StyleEngine.SetLocalStyleProperty(Node, "graphic", "graphic-properties", "stroke-color", OdfNamespaces.Svg, value ?? string.Empty, "svg");
        }
    }

    /// <summary>
    /// Gets or sets the shape stroke width, such as <c>1.5pt</c>.
    /// 取得或設定圖形的邊框寬度，例如 <c>1.5pt</c>。
    /// </summary>
    public string? StrokeWidth
    {
        get => Document.StyleEngine.GetStyleProperty(Node.GetAttribute("style-name", OdfNamespaces.Draw) ?? string.Empty, "stroke-width", OdfNamespaces.Svg, "graphic");
        set => Document.StyleEngine.SetLocalStyleProperty(Node, "graphic", "graphic-properties", "stroke-width", OdfNamespaces.Svg, value, "svg");
    }

    /// <summary>
    /// Gets or sets the shape stroke style, such as <c>solid</c>, <c>dash</c>, or <c>none</c>.
    /// 取得或設定圖形的邊框線條樣式，例如 <c>solid</c>、<c>dash</c> 或 <c>none</c>。
    /// </summary>
    public string? StrokeStyle
    {
        get => Document.StyleEngine.GetStyleProperty(Node.GetAttribute("style-name", OdfNamespaces.Draw) ?? string.Empty, "stroke", OdfNamespaces.Draw, "graphic");
        set => Document.StyleEngine.SetLocalStyleProperty(Node, "graphic", "graphic-properties", "stroke", OdfNamespaces.Draw, value, "draw");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfShape"/> class.
    /// 初始化 <see cref="OdfShape"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">The underlying <see cref="OdfNode"/> instance. / 底層的 <see cref="OdfNode"/> 執行個體。</param>
    /// <param name="slide">The owning slide instance. / 所屬的投影片執行個體。</param>
    public OdfShape(OdfNode node, OdfSlide slide) : this(node, slide?.Document!, slide)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfShape"/> class.
    /// 初始化 <see cref="OdfShape"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">The underlying <see cref="OdfNode"/> instance. / 底層的 <see cref="OdfNode"/> 執行個體。</param>
    /// <param name="doc">The owning ODF document instance. / 所屬的 ODF 文件執行個體。</param>
    public OdfShape(OdfNode node, OdfDocument doc) : this(node, doc, null)
    {
    }

    /// <summary>
    /// Adds an animation effect to the shape.
    /// 為圖形新增動畫效果。
    /// </summary>
    /// <param name="type">The animation type. / 動畫類型。</param>
    /// <param name="duration">The animation duration. / 動畫持續時間。</param>
    /// <param name="delay">The animation startup delay. / 動畫延遲啟動時間。</param>
    /// <exception cref="InvalidOperationException">When the shape does not belong to a slide. / 若圖形不屬於投影片則擲出。</exception>
    public void Animate(OdfAnimationType type, OdfLength duration, OdfLength delay)
    {
        if (Slide is null)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfShape_AnimationOnlySupportedPresentation"));
        }
        var slideNode = Slide.Node;
        var mainSeq = FindOrCreateAnimationSequence(slideNode);

        OdfNode stepPar = new(OdfNodeType.Element, "par", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
        stepPar.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "next", "smil");
        mainSeq.AppendChild(stepPar);

        string durStr = $"{duration.ToPoints() / 72.0:F2}s";
        string delayStr = $"{delay.ToPoints() / 72.0:F2}s";
        string targetId = Id;

        if (string.IsNullOrEmpty(targetId))
        {
            targetId = "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Id = targetId;
        }

        switch (type)
        {
            case OdfAnimationType.FadeIn:
                {
                    OdfNode filter = new(OdfNodeType.Element, "transitionFilter", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
                    filter.SetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", targetId, "smil");
                    filter.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "fade", "smil");
                    filter.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
                    filter.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", delayStr, "smil");
                    filter.SetAttribute("mode", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "in", "smil");
                    stepPar.AppendChild(filter);
                }
                break;
            case OdfAnimationType.FadeOut:
                {
                    OdfNode filter = new(OdfNodeType.Element, "transitionFilter", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
                    filter.SetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", targetId, "smil");
                    filter.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "fade", "smil");
                    filter.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
                    filter.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", delayStr, "smil");
                    filter.SetAttribute("mode", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "out", "smil");
                    stepPar.AppendChild(filter);
                }
                break;
            case OdfAnimationType.ZoomIn:
                {
                    OdfNode filter = new(OdfNodeType.Element, "transitionFilter", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
                    filter.SetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", targetId, "smil");
                    filter.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "zoom", "smil");
                    filter.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
                    filter.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", delayStr, "smil");
                    filter.SetAttribute("mode", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "in", "smil");
                    stepPar.AppendChild(filter);
                }
                break;
            case OdfAnimationType.WipeRight:
                {
                    OdfNode filter = new(OdfNodeType.Element, "transitionFilter", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
                    filter.SetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", targetId, "smil");
                    filter.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "wipe", "smil");
                    filter.SetAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "leftToRight", "smil");
                    filter.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
                    filter.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", delayStr, "smil");
                    filter.SetAttribute("mode", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "in", "smil");
                    stepPar.AppendChild(filter);
                }
                break;
        }
    }

    private static OdfNode FindOrCreateAnimationSequence(OdfNode slideNode)
    {
        const string AnimNs = "urn:oasis:names:tc:opendocument:xmlns:animation:1.0";

        OdfNode? timingRoot = null;
        foreach (var child in slideNode.Children)
        {
            if (child.NodeType is OdfNodeType.Element && child.LocalName is "par" && child.NamespaceUri is AnimNs &&
                child.GetAttribute("node-type", OdfNamespaces.Presentation) is "timing-root")
            {
                timingRoot = child;
                break;
            }
        }
        if (timingRoot is null)
        {
            timingRoot = new OdfNode(OdfNodeType.Element, "par", AnimNs, "anim");
            timingRoot.SetAttribute("node-type", OdfNamespaces.Presentation, "timing-root", "presentation");
            slideNode.AppendChild(timingRoot);
        }

        foreach (var child in timingRoot.Children)
        {
            if (child.NodeType is OdfNodeType.Element && child.LocalName is "seq" && child.NamespaceUri is AnimNs)
            {
                string? nodeType = child.GetAttribute("node-type", OdfNamespaces.Presentation);
                if (nodeType is "main-sequence")
                {
                    return child;
                }
            }
        }

        OdfNode mainSeq = new(OdfNodeType.Element, "seq", AnimNs, "anim");
        mainSeq.SetAttribute("node-type", OdfNamespaces.Presentation, "main-sequence", "presentation");
        timingRoot.AppendChild(mainSeq);
        return mainSeq;
    }
}
