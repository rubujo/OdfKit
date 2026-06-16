using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

public partial class PresentationDocument
{
    #region Presentation Slides

    /// <summary>
    /// 取得簡報的核心節點。
    /// </summary>
    /// <returns>簡報的 <see cref="OdfNode"/> 節點</returns>
    public OdfNode GetPresentationNode()
    {
        var body = FindChildElement(ContentRoot, "body", OdfNamespaces.Office);
        if (body is null)
        {
            body = new OdfNode(OdfNodeType.Element, "body", OdfNamespaces.Office, "office");
            ContentRoot.AppendChild(body);
        }

        var presentation = FindChildElement(body, "presentation", OdfNamespaces.Office);
        if (presentation is null)
        {
            presentation = new OdfNode(OdfNodeType.Element, "presentation", OdfNamespaces.Office, "office");
            body.AppendChild(presentation);
        }

        return presentation;
    }

    /// <summary>
    /// 新增一張投影片。
    /// </summary>
    /// <param name="name">投影片的名稱</param>
    /// <returns>新增的投影片執行個體</returns>
    public OdfSlide AddSlide(string? name = null)
    {
        var presentationNode = GetPresentationNode();
        var slideNode = new OdfNode(OdfNodeType.Element, "page", OdfNamespaces.Draw, "draw");

        string slideName = name ?? $"Slide {_slides.Count + 1}";
        slideNode.SetAttribute("name", OdfNamespaces.Draw, slideName, "draw");
        slideNode.SetAttribute("master-page-name", OdfNamespaces.Draw, "Default", "draw");

        presentationNode.AppendChild(slideNode);
        var slide = new OdfSlide(slideNode, this);
        _slides.Add(slide);
        return slide;
    }

    /// <summary>
    /// 複製指定的投影片。
    /// </summary>
    /// <param name="sourceSlideIndex">來源投影片的索引位置</param>
    /// <returns>複製後的新投影片執行個體</returns>
    /// <exception cref="ArgumentOutOfRangeException">索引超出範圍時拋出</exception>
    public OdfSlide CloneSlide(int sourceSlideIndex)
    {
        if (sourceSlideIndex < 0 || sourceSlideIndex >= _slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceSlideIndex));
        }

        var sourceSlide = _slides[sourceSlideIndex];
        var clonedNode = sourceSlide.Node.CloneNode(deep: true);

        string baseName = sourceSlide.Name;
        string newName = $"{baseName}_Clone";
        int count = 1;
        while (_slides.Exists(s => string.Equals(s.Name, newName, StringComparison.Ordinal)))
        {
            newName = $"{baseName}_Clone_{count++}";
        }
        clonedNode.SetAttribute("name", OdfNamespaces.Draw, newName, "draw");

        var presentationNode = GetPresentationNode();
        presentationNode.InsertAfter(clonedNode, sourceSlide.Node);

        ParseSlides();
        return _slides.Find(s => string.Equals(s.Name, newName, StringComparison.Ordinal))!;
    }

    /// <summary>
    /// 刪除指定的投影片。
    /// </summary>
    /// <param name="slideIndex">投影片的索引位置</param>
    /// <exception cref="ArgumentOutOfRangeException">索引超出範圍時拋出</exception>
    public void DeleteSlide(int slideIndex)
    {
        if (slideIndex < 0 || slideIndex >= _slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slideIndex));
        }

        var slide = _slides[slideIndex];
        var presentationNode = GetPresentationNode();
        presentationNode.RemoveChild(slide.Node);
        _slides.RemoveAt(slideIndex);
    }

    /// <summary>
    /// 移動投影片的順序位置。
    /// </summary>
    /// <param name="fromIndex">來源投影片的索引位置</param>
    /// <param name="toIndex">目標投影片的索引位置</param>
    /// <exception cref="ArgumentOutOfRangeException">索引超出範圍時拋出</exception>
    public void MoveSlide(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _slides.Count || toIndex < 0 || toIndex >= _slides.Count)
        {
            throw new ArgumentOutOfRangeException();
        }

        if (fromIndex == toIndex)
        {
            return;
        }

        var presentationNode = GetPresentationNode();
        var slideToMove = _slides[fromIndex];
        presentationNode.RemoveChild(slideToMove.Node);

        if (toIndex == _slides.Count - 1)
        {
            presentationNode.AppendChild(slideToMove.Node);
        }
        else
        {
            var refSlide = _slides[toIndex > fromIndex ? toIndex + 1 : toIndex];
            presentationNode.InsertBefore(slideToMove.Node, refSlide.Node);
        }

        ParseSlides();
    }

    /// <summary>
    /// 設定投影片的尺寸。
    /// </summary>
    /// <param name="width">寬度</param>
    /// <param name="height">高度</param>
    public void SetSlideSize(OdfLength width, OdfLength height)
    {
        var pageLayoutProps = GetDefaultPageLayoutProperties();
        pageLayoutProps.SetAttribute("page-width", OdfNamespaces.Fo, width.ToString(), "fo");
        pageLayoutProps.SetAttribute("page-height", OdfNamespaces.Fo, height.ToString(), "fo");
    }

    /// <summary>
    /// 設定投影片的方向。
    /// </summary>
    /// <param name="orientation">投影片方向列舉值</param>
    public void SetSlideOrientation(OdfPageOrientation orientation)
    {
        var pageLayoutProps = GetDefaultPageLayoutProperties();
        string orientationStr = orientation is OdfPageOrientation.Landscape ? "landscape" : "portrait";
        pageLayoutProps.SetAttribute("print-orientation", OdfNamespaces.Style, orientationStr, "style");

        string? wStr = pageLayoutProps.GetAttribute("page-width", OdfNamespaces.Fo);
        string? hStr = pageLayoutProps.GetAttribute("page-height", OdfNamespaces.Fo);

        if (!string.IsNullOrEmpty(wStr) && !string.IsNullOrEmpty(hStr))
        {
            var w = OdfLength.Parse(wStr);
            var h = OdfLength.Parse(hStr);

            if (orientation is OdfPageOrientation.Landscape && w.ToPoints() < h.ToPoints())
            {
                pageLayoutProps.SetAttribute("page-width", OdfNamespaces.Fo, h.ToString(), "fo");
                pageLayoutProps.SetAttribute("page-height", OdfNamespaces.Fo, w.ToString(), "fo");
            }
            else if (orientation is OdfPageOrientation.Portrait && w.ToPoints() > h.ToPoints())
            {
                pageLayoutProps.SetAttribute("page-width", OdfNamespaces.Fo, h.ToString(), "fo");
                pageLayoutProps.SetAttribute("page-height", OdfNamespaces.Fo, w.ToString(), "fo");
            }
        }
    }

    #endregion

    #region Slide Transitions

    /// <summary>
    /// 設定指定索引投影片的切換效果。
    /// </summary>
    /// <param name="slideIndex">投影片索引位置。</param>
    /// <param name="transition">投影片切換效果類型。</param>
    public void SetSlideTransition(int slideIndex, OdfSlideTransition transition)
    {
        if (slideIndex < 0 || slideIndex >= Slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slideIndex), "投影片索引超出範圍。");
        }

        var slide = Slides[slideIndex];
        var slideNode = slide.Node;

        if (transition == OdfSlideTransition.None)
        {
            slideNode.RemoveAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
            slideNode.RemoveAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
            slideNode.RemoveAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
            slideNode.RemoveAttribute("transition-type", OdfKit.Core.OdfNamespaces.Presentation);
        }
        else
        {
            OdfTransitionType type = transition switch
            {
                OdfSlideTransition.Push => OdfTransitionType.Push,
                OdfSlideTransition.Wipe => OdfTransitionType.Wipe,
                OdfSlideTransition.Zoom => OdfTransitionType.Zoom,
                _ => OdfTransitionType.Fade
            };
            slide.SetTransition(type, OdfLength.Parse("72pt"));
        }
    }

    #endregion

}
