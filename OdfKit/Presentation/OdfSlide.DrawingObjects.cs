using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

public partial class OdfSlide
{
    #region Slide Drawing Objects

    /// <summary>
    /// 在投影片上新增一個預留位置（Placeholder）。
    /// </summary>
    /// <param name="type">預留位置類型</param>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <returns>新增的預留位置圖形執行個體</returns>
    public OdfPlaceholder AddPlaceholder(OdfPlaceholderType type, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        OdfNode shapeNode = new(OdfNodeType.Element, "rect", OdfNamespaces.Draw, "draw");
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        shapeNode.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        shapeNode.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");

        Node.AppendChild(shapeNode);
        var placeholder = new OdfPlaceholder(shapeNode, this)
        {
            PlaceholderType = type
        };
        return placeholder;
    }

    /// <summary>
    /// 在投影片上新增內嵌物件（如其他文件或子組件）。
    /// </summary>
    /// <param name="subPath">內嵌物件於套件內的路徑</param>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <returns>新增內嵌物件圖形執行個體</returns>
    public OdfShape AddEmbeddedObject(string subPath, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        var frame = CreateDrawingFrame(x, y, w, h);
        OdfNode objNode = new(OdfNodeType.Element, "object", OdfNamespaces.Draw, "draw");

        string href = subPath;
        if (!href.StartsWith("./"))
        {
            href = "./" + href;
        }
        if (href.EndsWith("/"))
        {
            href = href.Substring(0, href.Length - 1);
        }

        objNode.SetAttribute("href", OdfNamespaces.XLink, href, "xlink");
        objNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        objNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        objNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");

        frame.AppendChild(objNode);
        Node.AppendChild(frame);
        return new OdfShape(frame, this);
    }

    /// <summary>
    /// 在投影片上新增文字方塊。
    /// </summary>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <param name="text">文字內容</param>
    /// <returns>新增的文字方塊圖形執行個體</returns>
    public OdfTextBox AddTextBox(OdfLength x, OdfLength y, OdfLength w, OdfLength h, string text)
        => AddTextBox(x, y, w, h, new[] { text });

    /// <summary>
    /// 在投影片上新增多段落文字方塊。
    /// </summary>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <param name="paragraphs">段落文字集合。</param>
    /// <returns>新增的文字方塊圖形執行個體</returns>
    public OdfTextBox AddTextBox(OdfLength x, OdfLength y, OdfLength w, OdfLength h, IEnumerable<string> paragraphs)
    {
        if (paragraphs is null)
            throw new ArgumentNullException(nameof(paragraphs));

        var frame = CreateDrawingFrame(x, y, w, h);
        OdfNode textBoxNode = new(OdfNodeType.Element, "text-box", OdfNamespaces.Draw, "draw");
        frame.AppendChild(textBoxNode);

        bool addedParagraph = false;
        foreach (string paragraph in paragraphs)
        {
            OdfNode pNode = new(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            pNode.TextContent = paragraph ?? string.Empty;
            textBoxNode.AppendChild(pNode);
            addedParagraph = true;
        }

        if (!addedParagraph)
        {
            textBoxNode.AppendChild(new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text"));
        }

        Node.AppendChild(frame);
        return new OdfTextBox(frame, this);
    }
    /// <summary>
    /// 在投影片上新增影片物件。
    /// </summary>
    /// <param name="packagePath">影片在封裝包內的路徑。</param>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="width">寬度。</param>
    /// <param name="height">高度。</param>
    /// <param name="mimeType">影片 MIME 類型。</param>
    /// <returns>新建立的媒體物件。</returns>
    public OdfMediaObject AddVideo(
        string packagePath,
        OdfLength x,
        OdfLength y,
        OdfLength width,
        OdfLength height,
        string mimeType = "video/mp4")
    {
        return AddMedia(packagePath, x, y, width, height, mimeType);
    }

    /// <summary>
    /// 在投影片上新增音訊物件。
    /// </summary>
    /// <param name="packagePath">音訊在封裝包內的路徑。</param>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="width">寬度。</param>
    /// <param name="height">高度。</param>
    /// <param name="mimeType">音訊 MIME 類型。</param>
    /// <returns>新建立的媒體物件。</returns>
    public OdfMediaObject AddAudio(
        string packagePath,
        OdfLength x,
        OdfLength y,
        OdfLength width,
        OdfLength height,
        string mimeType = "audio/mpeg")
    {
        return AddMedia(packagePath, x, y, width, height, mimeType);
    }

    private OdfMediaObject AddMedia(
        string packagePath,
        OdfLength x,
        OdfLength y,
        OdfLength width,
        OdfLength height,
        string mimeType)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new ArgumentException("封裝路徑不可為空白。", nameof(packagePath));
        if (string.IsNullOrWhiteSpace(mimeType))
            throw new ArgumentException("MIME 類型不可為空白。", nameof(mimeType));

        var frame = CreateDrawingFrame(x, y, width, height);
        var plugin = new OdfNode(OdfNodeType.Element, "plugin", OdfNamespaces.Draw, "draw");
        plugin.SetAttribute("href", OdfNamespaces.XLink, packagePath, "xlink");
        plugin.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        plugin.SetAttribute("mime-type", OdfNamespaces.Draw, mimeType, "draw");
        frame.AppendChild(plugin);
        Node.AppendChild(frame);

        return new OdfMediaObject(packagePath, mimeType);
    }

    /// <summary>
    /// 在投影片上新增基本圖形。
    /// </summary>
    /// <param name="shapeType">圖形類型</param>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <returns>新增的圖形執行個體</returns>
    public OdfShape AddShape(OdfShapeType shapeType, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        string localName = shapeType switch
        {
            OdfShapeType.Rectangle => "rect",
            OdfShapeType.Ellipse => "ellipse",
            _ => "custom-shape"
        };

        OdfNode shapeNode = new(OdfNodeType.Element, localName, OdfNamespaces.Draw, "draw");
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        shapeNode.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        shapeNode.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");

        Node.AppendChild(shapeNode);
        return new OdfShape(shapeNode, this);
    }

    /// <summary>
    /// 在投影片上新增直線圖形。
    /// </summary>
    /// <param name="x1">起點 X 軸座標位置。</param>
    /// <param name="y1">起點 Y 軸座標位置。</param>
    /// <param name="x2">終點 X 軸座標位置。</param>
    /// <param name="y2">終點 Y 軸座標位置。</param>
    /// <returns>新增的直線圖形執行個體。</returns>
    public OdfShape AddLine(OdfLength x1, OdfLength y1, OdfLength x2, OdfLength y2)
    {
        OdfNode shapeNode = new(OdfNodeType.Element, "line", OdfNamespaces.Draw, "draw");
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        shapeNode.SetAttribute("x1", OdfNamespaces.Svg, x1.ToString(), "svg");
        shapeNode.SetAttribute("y1", OdfNamespaces.Svg, y1.ToString(), "svg");
        shapeNode.SetAttribute("x2", OdfNamespaces.Svg, x2.ToString(), "svg");
        shapeNode.SetAttribute("y2", OdfNamespaces.Svg, y2.ToString(), "svg");

        Node.AppendChild(shapeNode);
        return new OdfShape(shapeNode, this);
    }

    /// <summary>
    /// 在投影片上新增折線圖形。
    /// </summary>
    /// <param name="points">點座標集合</param>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <returns>新增的折線圖形執行個體</returns>
    public OdfShape AddPolyline(IEnumerable<System.Drawing.PointF> points, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        OdfNode shapeNode = new(OdfNodeType.Element, "polyline", OdfNamespaces.Draw, "draw");
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        shapeNode.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        shapeNode.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");

        var pointsStr = string.Join(" ", points.Select(p => $"{p.X.ToString(System.Globalization.CultureInfo.InvariantCulture)},{p.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
        shapeNode.SetAttribute("points", OdfNamespaces.Draw, pointsStr, "draw");

        Node.AppendChild(shapeNode);
        return new OdfShape(shapeNode, this);
    }

    /// <summary>
    /// 在投影片上新增圖片。
    /// </summary>
    /// <param name="imageBytes">圖片的位元組陣列</param>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <param name="altText">選用的圖片替代文字。</param>
    /// <returns>新增的圖片圖形執行個體</returns>
    public OdfPicture AddPicture(byte[] imageBytes, OdfLength x, OdfLength y, OdfLength w, OdfLength h, string? altText = null)
    {
        var frame = CreateDrawingFrame(x, y, w, h);

        OdfMediaManager mediaManager = new(Document.Package);
        string imageHref = mediaManager.AddImage(imageBytes, "slide_image.png");

        OdfNode imgNode = new(OdfNodeType.Element, "image", OdfNamespaces.Draw, "draw");
        imgNode.SetAttribute("href", OdfNamespaces.XLink, imageHref, "xlink");
        imgNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        imgNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        imgNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");

        frame.AppendChild(imgNode);
        Node.AppendChild(frame);
        var picture = new OdfPicture(frame, this);
        picture.AltText = altText;
        return picture;
    }

    /// <summary>
    /// 設定投影片切換動畫效果。
    /// </summary>
    /// <param name="type">切換效果類型</param>
    /// <param name="duration">持續時間</param>
    public void SetTransition(OdfTransitionType type, OdfLength duration)
        => SetTransition(type, duration, OdfTransitionSpeed.Medium);

    /// <summary>
    /// 設定投影片切換動畫效果與速度。
    /// </summary>
    /// <param name="type">切換效果類型。</param>
    /// <param name="duration">持續時間。</param>
    /// <param name="speed">切換速度。</param>
    public void SetTransition(OdfTransitionType type, OdfLength duration, OdfTransitionSpeed speed)
    {
        string durStr = $"{duration.ToPoints() / 72.0:F2}s";

        switch (type)
        {
            case OdfTransitionType.Fade:
                Node.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "fade", "smil");
                Node.SetAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "fadeOverColor", "smil");
                break;
            case OdfTransitionType.Push:
                Node.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "push", "smil");
                Node.SetAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "fromBottom", "smil");
                break;
            case OdfTransitionType.Wipe:
                Node.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "wipe", "smil");
                Node.SetAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "leftToRight", "smil");
                break;
            case OdfTransitionType.Zoom:
                Node.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "zoom", "smil");
                Node.SetAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "in", "smil");
                break;
            case OdfTransitionType.Split:
                Node.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "split", "smil");
                Node.SetAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "horizontalOut", "smil");
                break;
        }

        Node.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
        Node.SetAttribute("transition-type", OdfNamespaces.Presentation, "automatic", "presentation");
        Node.SetAttribute("transition-speed", OdfNamespaces.Presentation, speed switch
        {
            OdfTransitionSpeed.Slow => "slow",
            OdfTransitionSpeed.Fast => "fast",
            _ => "medium",
        }, "presentation");
    }

    private OdfNode CreateDrawingFrame(OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        OdfNode frame = new(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("id", OdfNamespaces.Draw, "frm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        frame.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
        frame.SetAttribute("anchor-type", OdfNamespaces.Draw, "page", "draw");
        return frame;
    }

    private IReadOnlyList<T> FindDrawingObjects<T>(Func<OdfNode, bool> predicate, Func<OdfNode, T> factory)
    {
        List<T> objects = [];
        foreach (OdfNode child in Node.Children)
        {
            if (child.NodeType is OdfNodeType.Element && predicate(child))
            {
                objects.Add(factory(child));
            }
        }

        return objects.AsReadOnly();
    }

    private static bool ContainsDescendant(OdfNode node, string localName, string namespaceUri)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return true;
            }

            if (ContainsDescendant(child, localName, namespaceUri))
            {
                return true;
            }
        }

        return false;
    }

    #endregion
}
