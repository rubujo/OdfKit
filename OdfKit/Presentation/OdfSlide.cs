using System;
using System.Collections;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

/// <summary>
/// Represents a presentation slide.
/// 表示簡報投影片（Slide）的類別。
/// </summary>
/// <param name="node">The underlying <see cref="OdfNode"/> instance. / 底層的 <see cref="OdfNode"/> 執行個體。</param>
/// <param name="doc">The owning presentation document instance. / 所屬的簡報文件執行個體。</param>
public partial class OdfSlide(OdfNode node, PresentationDocument doc)
{
    /// <summary>
    /// 取得底層的 ODF 節點。
    /// </summary>
    internal OdfNode Node { get; } = node;

    /// <summary>
    /// Gets the owning presentation document.
    /// 取得所屬的簡報文件。
    /// </summary>
    public PresentationDocument Document { get; } = doc;

    /// <summary>
    /// Gets or sets the slide name.
    /// 取得或設定投影片名稱。
    /// </summary>
    public string Name
    {
        get => Node.GetAttribute("name", OdfNamespaces.Draw) ?? string.Empty;
        set => Node.SetAttribute("name", OdfNamespaces.Draw, value, "draw");
    }

    /// <summary>
    /// Gets or sets the master page name used by the slide.
    /// 取得或設定投影片使用的母片名稱。
    /// </summary>
    public string MasterPageName
    {
        get => Node.GetAttribute("master-page-name", OdfNamespaces.Draw) ?? string.Empty;
        set => Node.SetAttribute("master-page-name", OdfNamespaces.Draw, value, "draw");
    }

    /// <summary>
    /// Gets or sets the page layout name used by the slide.
    /// 取得或設定投影片使用的版面配置名稱。
    /// </summary>
    public string? PresentationPageLayoutName
    {
        get => Node.GetAttribute("presentation-page-layout-name", OdfNamespaces.Presentation);
        set
        {
            if (value is null)
            {
                Node.RemoveAttribute("presentation-page-layout-name", OdfNamespaces.Presentation);
            }
            else
            {
                Node.SetAttribute("presentation-page-layout-name", OdfNamespaces.Presentation, value, "presentation");
            }
        }
    }

    /// <summary>
    /// Gets or sets the slide background color, such as <c>#FFFFFF</c>.
    /// 取得或設定投影片背景色（例如 <c>#FFFFFF</c>）。
    /// </summary>
    public string? BackgroundColor
    {
        get
        {
            string? styleName = Node.GetAttribute("style-name", OdfNamespaces.Draw);
            return string.IsNullOrWhiteSpace(styleName)
                ? null
                : Document.StyleEngine.GetStyleProperty(styleName!, "fill-color", OdfNamespaces.Draw, "drawing-page");
        }
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Document.StyleEngine.SetLocalStyleProperty(Node, "drawing-page", "drawing-page-properties", "fill", OdfNamespaces.Draw, null, "draw");
                Document.StyleEngine.SetLocalStyleProperty(Node, "drawing-page", "drawing-page-properties", "fill-color", OdfNamespaces.Draw, null, "draw");
                return;
            }

            Document.StyleEngine.SetLocalStyleProperty(Node, "drawing-page", "drawing-page-properties", "fill", OdfNamespaces.Draw, "solid", "draw");
            Document.StyleEngine.SetLocalStyleProperty(Node, "drawing-page", "drawing-page-properties", "fill-color", OdfNamespaces.Draw, value, "draw");
        }
    }

    /// <summary>
    /// Gets or sets the style name used by the slide header.
    /// 取得或設定投影片頁首使用的樣式名稱。
    /// </summary>
    public string? UseHeaderName
    {
        get => Node.GetAttribute("use-header-name", OdfNamespaces.Presentation);
        set
        {
            if (value is null)
            {
                Node.RemoveAttribute("use-header-name", OdfNamespaces.Presentation);
            }
            else
            {
                Node.SetAttribute("use-header-name", OdfNamespaces.Presentation, value, "presentation");
            }
        }
    }

    /// <summary>
    /// Gets or sets the style name used by the slide footer.
    /// 取得或設定投影片頁尾使用的樣式名稱。
    /// </summary>
    public string? UseFooterName
    {
        get => Node.GetAttribute("use-footer-name", OdfNamespaces.Presentation);
        set
        {
            if (value is null)
            {
                Node.RemoveAttribute("use-footer-name", OdfNamespaces.Presentation);
            }
            else
            {
                Node.SetAttribute("use-footer-name", OdfNamespaces.Presentation, value, "presentation");
            }
        }
    }

    /// <summary>
    /// Gets or sets the style name used by the slide date and time field.
    /// 取得或設定投影片日期與時間使用的樣式名稱。
    /// </summary>
    public string? UseDateTimeName
    {
        get => Node.GetAttribute("use-date-time-name", OdfNamespaces.Presentation);
        set
        {
            if (value is null)
            {
                Node.RemoveAttribute("use-date-time-name", OdfNamespaces.Presentation);
            }
            else
            {
                Node.SetAttribute("use-date-time-name", OdfNamespaces.Presentation, value, "presentation");
            }
        }
    }

    /// <summary>
    /// Gets the slide notes page.
    /// 取得投影片的備忘錄頁面（Notes Page）。
    /// </summary>
    public OdfNotesPage SpeakerNotesPage
    {
        get
        {
            var notesNode = Node.FindChildElement("notes", OdfNamespaces.Presentation);
            if (notesNode is null)
            {
                notesNode = new OdfNode(OdfNodeType.Element, "notes", OdfNamespaces.Presentation, "presentation");
                Node.AppendChild(notesNode);
            }
            return new OdfNotesPage(notesNode, this);
        }
    }

    /// <summary>
    /// Gets or sets the slide speaker notes text.
    /// 取得或設定投影片備忘錄文字。
    /// </summary>
    public string SpeakerNotes
    {
        get => SpeakerNotesPage.SpeakerNotesText;
        set => SpeakerNotesPage.SpeakerNotesText = value;
    }

    /// <summary>
    /// Gets the paragraph text of the slide speaker notes.
    /// 取得投影片備忘錄的段落文字。
    /// </summary>
    public IReadOnlyList<string> SpeakerNoteParagraphs => SpeakerNotesPage.SpeakerNoteParagraphs;

    /// <summary>
    /// Sets slide speaker notes as multiple paragraphs.
    /// 以多段落形式設定投影片備忘錄文字。
    /// </summary>
    /// <param name="paragraphs">The paragraph text collection. / 段落文字集合。</param>
    /// <returns>The current slide. / 目前投影片。</returns>
    public OdfSlide SetSpeakerNotes(IEnumerable<string> paragraphs)
    {
        SpeakerNotesPage.SetSpeakerNotes(paragraphs);
        return this;
    }

    /// <summary>
    /// Gets the animation root node.
    /// 取得動畫根節點。
    /// </summary>
    public OdfAnimationNode AnimationRoot
    {
        get
        {
            const string AnimNs = "urn:oasis:names:tc:opendocument:xmlns:animation:1.0";

            OdfNode? timingRoot = null;
            foreach (var child in Node.Children)
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
                Node.AppendChild(timingRoot);
            }

            OdfNode? mainSeq = null;
            foreach (var child in timingRoot.Children)
            {
                if (child.NodeType is OdfNodeType.Element && child.LocalName is "seq" && child.NamespaceUri is AnimNs)
                {
                    string? nodeType = child.GetAttribute("node-type", OdfNamespaces.Presentation);
                    if (nodeType is "main-sequence")
                    {
                        mainSeq = child;
                        break;
                    }
                }
            }
            if (mainSeq is null)
            {
                mainSeq = new OdfNode(OdfNodeType.Element, "seq", AnimNs, "anim");
                mainSeq.SetAttribute("node-type", OdfNamespaces.Presentation, "main-sequence", "presentation");
                timingRoot.AppendChild(mainSeq);
            }
            return new OdfAnimationNode(mainSeq);
        }
    }

    /// <summary>
    /// Gets the summary list of all placeholders in the slide.
    /// 取得投影片中所有預留位置的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfPlaceholderInfo> GetPlaceholderInfos() =>
        OdfSlidePlaceholderReadEngine.GetPlaceholders(this);

    /// <summary>
    /// Gets the read-only list of all placeholders in the slide.
    /// 取得投影片中所有預留位置的唯讀清單。
    /// </summary>
    public IReadOnlyList<OdfPlaceholder> Placeholders
    {
        get
        {
            List<OdfPlaceholder> list = [];
            foreach (var child in Node.Children)
            {
                if (child.NodeType is OdfNodeType.Element && child.NamespaceUri == OdfNamespaces.Draw)
                {
                    string? ph = child.GetAttribute("placeholder", OdfNamespaces.Presentation);
                    if (ph is "true")
                    {
                        list.Add(new OdfPlaceholder(child, this));
                    }
                }
            }
            return list.AsReadOnly();
        }
    }

    /// <summary>
    /// Gets the text boxes on the slide.
    /// 取得投影片上的文字方塊清單。
    /// </summary>
    public IReadOnlyList<OdfTextBox> TextBoxes => FindDrawingObjects(
        node => node.NamespaceUri == OdfNamespaces.Draw &&
            ContainsDescendant(node, "text-box", OdfNamespaces.Draw),
        node => new OdfTextBox(node, this));

    /// <summary>
    /// Gets the pictures on the slide.
    /// 取得投影片上的圖片清單。
    /// </summary>
    public IReadOnlyList<OdfPicture> Pictures => FindDrawingObjects(
        node => node.NamespaceUri == OdfNamespaces.Draw &&
            ContainsDescendant(node, "image", OdfNamespaces.Draw),
        node => new OdfPicture(node, this));

    /// <summary>
    /// Gets the general shapes on the slide.
    /// 取得投影片上的一般圖形清單。
    /// </summary>
    public IReadOnlyList<OdfShape> Shapes => FindDrawingObjects(
        node => node.NamespaceUri == OdfNamespaces.Draw &&
            node.LocalName is "rect" or "ellipse" or "custom-shape" or "line" or "connector" or "polyline",
        node => new OdfShape(node, this));
}
