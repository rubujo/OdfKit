using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfNamespaces = OdfKit.Core.OdfNamespaces;

namespace OdfKit.Presentation;

/// <summary>
/// 表示投影片備忘錄頁面（Speaker Notes Page）的類別。
/// </summary>
/// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
/// <param name="slide">所屬的投影片執行個體</param>
public class OdfNotesPage(OdfNode node, OdfSlide slide)
{
    /// <summary>
    /// 取得底層的 ODF 節點。
    /// </summary>
    public OdfNode Node { get; } = node;

    /// <summary>
    /// 取得所屬的投影片。
    /// </summary>
    public OdfSlide Slide { get; } = slide;

    /// <summary>
    /// 取得或設定主講人備忘錄的文字內容。
    /// </summary>
    public string SpeakerNotesText
    {
        get
        {
            var textBox = FindTextBoxInNotes(Node);
            return textBox?.TextContent ?? string.Empty;
        }
        set
        {
            var textBox = FindTextBoxInNotes(Node);
            if (textBox is null)
            {
                OdfNode frame = new(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
                frame.SetAttribute("class", OdfNamespaces.Presentation, "notes", "presentation");
                frame.SetAttribute("x", OdfNamespaces.Svg, "2cm", "svg");
                frame.SetAttribute("y", OdfNamespaces.Svg, "15cm", "svg");
                frame.SetAttribute("width", OdfNamespaces.Svg, "20cm", "svg");
                frame.SetAttribute("height", OdfNamespaces.Svg, "10cm", "svg");

                OdfNode box = new(OdfNodeType.Element, "text-box", OdfNamespaces.Draw, "draw");
                frame.AppendChild(box);
                Node.AppendChild(frame);
                textBox = box;
            }

            textBox.Children.Clear();
            OdfNode p = new(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            p.TextContent = value;
            textBox.AppendChild(p);
        }
    }

    /// <summary>
    /// 取得或設定備忘錄頁面所使用的母片名稱。
    /// </summary>
    public string? MasterPageName
    {
        get => Node.GetAttribute("master-page-name", OdfNamespaces.Draw);
        set
        {
            if (value is null)
            {
                Node.RemoveAttribute("master-page-name", OdfNamespaces.Draw);
            }
            else
            {
                Node.SetAttribute("master-page-name", OdfNamespaces.Draw, value, "draw");
            }
        }
    }

    /// <summary>
    /// 取得備忘錄頁面上所有圖形（不含縮圖）的唯讀清單。
    /// </summary>
    public IReadOnlyList<OdfShape> Shapes
    {
        get
        {
            List<OdfShape> list = [];
            foreach (var child in Node.Children)
            {
                if (child.NodeType is OdfNodeType.Element && child.NamespaceUri == OdfNamespaces.Draw && child.LocalName is not "page-thumbnail")
                {
                    if (child.LocalName is "frame" && child.FindChildElement("text-box", OdfNamespaces.Draw) is not null)
                    {
                        list.Add(new OdfTextBox(child, Slide));
                    }
                    else if (child.LocalName is "frame" && child.FindChildElement("image", OdfNamespaces.Draw) is not null)
                    {
                        list.Add(new OdfPicture(child, Slide));
                    }
                    else
                    {
                        list.Add(new OdfShape(child, Slide));
                    }
                }
            }
            return list.AsReadOnly();
        }
    }

    /// <summary>
    /// 在備忘錄頁面上新增文字方塊。
    /// </summary>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <param name="text">文字內容</param>
    /// <returns>新增的文字方塊圖形執行個體</returns>
    public OdfTextBox AddTextBox(OdfLength x, OdfLength y, OdfLength w, OdfLength h, string text)
    {
        OdfNode frame = new(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("id", OdfNamespaces.Draw, "frm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        frame.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
        frame.SetAttribute("anchor-type", OdfNamespaces.Draw, "page", "draw");

        OdfNode textBoxNode = new(OdfNodeType.Element, "text-box", OdfNamespaces.Draw, "draw");
        frame.AppendChild(textBoxNode);

        OdfNode pNode = new(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        pNode.TextContent = text;
        textBoxNode.AppendChild(pNode);

        Node.AppendChild(frame);
        return new OdfTextBox(frame, Slide);
    }

    /// <summary>
    /// 在備忘錄頁面上新增投影片縮圖。
    /// </summary>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    public void AddSlideThumbnail(OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        OdfNode thumbnail = new(OdfNodeType.Element, "page-thumbnail", OdfNamespaces.Draw, "draw");
        thumbnail.SetAttribute("id", OdfNamespaces.Draw, "thm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        thumbnail.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        thumbnail.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        thumbnail.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        thumbnail.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
        thumbnail.SetAttribute("page-number", OdfNamespaces.Draw, Slide.Name, "draw");
        Node.AppendChild(thumbnail);
    }

    /// <summary>
    /// 在備忘錄頁面上新增基本圖形。
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
        return new OdfShape(shapeNode, Slide.Document);
    }

    /// <summary>
    /// 在備忘錄頁面上新增圖片。
    /// </summary>
    /// <param name="imageBytes">圖片的位元組陣列</param>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <returns>新增的圖片圖形執行個體</returns>
    public OdfPicture AddPicture(byte[] imageBytes, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        OdfNode frame = new(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("id", OdfNamespaces.Draw, "frm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        frame.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
        frame.SetAttribute("anchor-type", OdfNamespaces.Draw, "page", "draw");

        OdfMediaManager mediaManager = new(Slide.Document.Package);
        string imageHref = mediaManager.AddImage(imageBytes, "notes_image.png");

        OdfNode imgNode = new(OdfNodeType.Element, "image", OdfNamespaces.Draw, "draw");
        imgNode.SetAttribute("href", OdfNamespaces.XLink, imageHref, "xlink");
        imgNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        imgNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        imgNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");

        frame.AppendChild(imgNode);
        Node.AppendChild(frame);
        return new OdfPicture(frame, Slide.Document);
    }

    private static OdfNode? FindTextBoxInNotes(OdfNode notesNode)
    {
        foreach (var frame in notesNode.Children)
        {
            if (frame.NodeType is OdfNodeType.Element && frame.LocalName is "frame" && frame.NamespaceUri == OdfNamespaces.Draw)
            {
                string? cls = frame.GetAttribute("class", OdfNamespaces.Presentation);
                if (cls is "notes")
                {
                    foreach (var child in frame.Children)
                    {
                        if (child.NodeType is OdfNodeType.Element && child.LocalName is "text-box" && child.NamespaceUri == OdfNamespaces.Draw)
                        {
                            return child;
                        }
                    }
                }
            }
        }
        return null;
    }
}

/// <summary>
/// 表示投影片講義頁面（Handout Page）的類別。
/// </summary>
/// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
/// <param name="doc">所屬的簡報文件執行個體</param>
public class OdfHandoutPage(OdfNode node, PresentationDocument doc)
{
    /// <summary>
    /// 取得底層的 ODF 節點。
    /// </summary>
    public OdfNode Node { get; } = node;

    /// <summary>
    /// 取得所屬的簡報文件。
    /// </summary>
    public PresentationDocument Document { get; } = doc;

    /// <summary>
    /// 取得或設定講義頁面的名稱。
    /// </summary>
    public string? Name
    {
        get => Node.GetAttribute("name", OdfNamespaces.Style);
        set
        {
            if (value is null)
            {
                Node.RemoveAttribute("name", OdfNamespaces.Style);
            }
            else
            {
                Node.SetAttribute("name", OdfNamespaces.Style, value, "style");
            }
        }
    }

    /// <summary>
    /// 取得或設定講義頁面所使用的母片名稱。
    /// </summary>
    public string? MasterPageName
    {
        get => Node.GetAttribute("master-page-name", OdfNamespaces.Draw);
        set
        {
            if (value is null)
            {
                Node.RemoveAttribute("master-page-name", OdfNamespaces.Draw);
            }
            else
            {
                Node.SetAttribute("master-page-name", OdfNamespaces.Draw, value, "draw");
            }
        }
    }

    /// <summary>
    /// 取得講義頁面上所有圖形（不含縮圖預留位置）的唯讀清單。
    /// </summary>
    public IReadOnlyList<OdfShape> Shapes
    {
        get
        {
            List<OdfShape> list = [];
            foreach (var child in Node.Children)
            {
                if (child.NodeType is OdfNodeType.Element && child.NamespaceUri == OdfNamespaces.Draw && child.LocalName is not "page-thumbnail")
                {
                    if (child.LocalName is "frame" && child.FindChildElement("text-box", OdfNamespaces.Draw) is not null)
                    {
                        list.Add(new OdfTextBox(child, Document));
                    }
                    else if (child.LocalName is "frame" && child.FindChildElement("image", OdfNamespaces.Draw) is not null)
                    {
                        list.Add(new OdfPicture(child, Document));
                    }
                    else
                    {
                        list.Add(new OdfShape(child, Document));
                    }
                }
            }
            return list.AsReadOnly();
        }
    }

    /// <summary>
    /// 在講義頁面上新增文字方塊。
    /// </summary>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <param name="text">文字內容</param>
    /// <returns>新增的文字方塊圖形執行個體</returns>
    public OdfTextBox AddTextBox(OdfLength x, OdfLength y, OdfLength w, OdfLength h, string text)
    {
        OdfNode frame = new(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("id", OdfNamespaces.Draw, "frm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        frame.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
        frame.SetAttribute("anchor-type", OdfNamespaces.Draw, "page", "draw");

        OdfNode textBoxNode = new(OdfNodeType.Element, "text-box", OdfNamespaces.Draw, "draw");
        frame.AppendChild(textBoxNode);

        OdfNode pNode = new(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        pNode.TextContent = text;
        textBoxNode.AppendChild(pNode);

        Node.AppendChild(frame);
        return new OdfTextBox(frame, Document);
    }

    /// <summary>
    /// 在講義頁面上新增投影片縮圖預留位置。
    /// </summary>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    public void AddSlideThumbnailPlaceholder(OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        OdfNode thumbnail = new(OdfNodeType.Element, "page-thumbnail", OdfNamespaces.Draw, "draw");
        thumbnail.SetAttribute("id", OdfNamespaces.Draw, "thm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        thumbnail.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        thumbnail.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        thumbnail.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        thumbnail.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
        Node.AppendChild(thumbnail);
    }
}
