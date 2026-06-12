using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation
{
    public enum OdfPageOrientation
    {
        Landscape,
        Portrait
    }

    public enum OdfShapeType
    {
        Rectangle,
        Ellipse,
        Custom
    }

    public enum OdfTransitionType
    {
        Fade,
        Push,
        Wipe,
        Zoom,
        Split
    }

    public enum OdfAnimationType
    {
        FadeIn,
        FadeOut,
        ZoomIn,
        WipeRight
    }

    public class PresentationDocument : OdfDocument
    {
        private readonly List<OdfSlide> _slides = new();

        public IReadOnlyList<OdfSlide> Slides => _slides.AsReadOnly();

        public PresentationDocument() : this(OdfPackage.Create(new MemoryStream()))
        {
            Package.SetMimeType("application/vnd.oasis.opendocument.presentation");
            Package.SaveManifestToEntries();
        }

        public PresentationDocument(OdfPackage package) : base(package)
        {
            if (string.IsNullOrEmpty(package.MimeType))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.presentation");
            }
            ParseSlides();
        }

        private void ParseSlides()
        {
            _slides.Clear();
            var presentationNode = GetPresentationNode();
            foreach (var child in presentationNode.Children)
            {
                if (child.NodeType == OdfNodeType.Element && child.LocalName == "page" && child.NamespaceUri == OdfNamespaces.Draw)
                {
                    _slides.Add(new OdfSlide(child, this));
                }
            }
        }

        public OdfNode GetPresentationNode()
        {
            var body = FindChildElement(ContentRoot, "body", OdfNamespaces.Office);
            if (body == null)
            {
                body = new OdfNode(OdfNodeType.Element, "body", OdfNamespaces.Office, "office");
                ContentRoot.AppendChild(body);
            }

            var presentation = FindChildElement(body, "presentation", OdfNamespaces.Office);
            if (presentation == null)
            {
                presentation = new OdfNode(OdfNodeType.Element, "presentation", OdfNamespaces.Office, "office");
                body.AppendChild(presentation);
            }

            return presentation;
        }

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

        public OdfSlide CloneSlide(int sourceSlideIndex)
        {
            if (sourceSlideIndex < 0 || sourceSlideIndex >= _slides.Count)
                throw new ArgumentOutOfRangeException(nameof(sourceSlideIndex));

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

        public void DeleteSlide(int slideIndex)
        {
            if (slideIndex < 0 || slideIndex >= _slides.Count)
                throw new ArgumentOutOfRangeException(nameof(slideIndex));

            var slide = _slides[slideIndex];
            var presentationNode = GetPresentationNode();
            presentationNode.RemoveChild(slide.Node);
            _slides.RemoveAt(slideIndex);
        }

        public void MoveSlide(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _slides.Count || toIndex < 0 || toIndex >= _slides.Count)
                throw new ArgumentOutOfRangeException();

            if (fromIndex == toIndex) return;

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

        public void SetSlideSize(OdfLength width, OdfLength height)
        {
            var pageLayoutProps = GetDefaultPageLayoutProperties();
            pageLayoutProps.SetAttribute("page-width", OdfNamespaces.Fo, width.ToString(), "fo");
            pageLayoutProps.SetAttribute("page-height", OdfNamespaces.Fo, height.ToString(), "fo");
        }

        public void SetSlideOrientation(OdfPageOrientation orientation)
        {
            var pageLayoutProps = GetDefaultPageLayoutProperties();
            string orientationStr = orientation == OdfPageOrientation.Landscape ? "landscape" : "portrait";
            pageLayoutProps.SetAttribute("print-orientation", OdfNamespaces.Style, orientationStr, "style");

            string? wStr = pageLayoutProps.GetAttribute("page-width", OdfNamespaces.Fo);
            string? hStr = pageLayoutProps.GetAttribute("page-height", OdfNamespaces.Fo);

            if (!string.IsNullOrEmpty(wStr) && !string.IsNullOrEmpty(hStr))
            {
                var w = OdfLength.Parse(wStr);
                var h = OdfLength.Parse(hStr);

                if (orientation == OdfPageOrientation.Landscape && w.ToPoints() < h.ToPoints())
                {
                    pageLayoutProps.SetAttribute("page-width", OdfNamespaces.Fo, h.ToString(), "fo");
                    pageLayoutProps.SetAttribute("page-height", OdfNamespaces.Fo, w.ToString(), "fo");
                }
                else if (orientation == OdfPageOrientation.Portrait && w.ToPoints() > h.ToPoints())
                {
                    pageLayoutProps.SetAttribute("page-width", OdfNamespaces.Fo, h.ToString(), "fo");
                    pageLayoutProps.SetAttribute("page-height", OdfNamespaces.Fo, w.ToString(), "fo");
                }
            }
        }

        private OdfNode GetDefaultPageLayoutProperties()
        {
            var autoStyles = FindChildElement(StylesRoot, "automatic-styles", OdfNamespaces.Office);
            if (autoStyles == null)
            {
                autoStyles = new OdfNode(OdfNodeType.Element, "automatic-styles", OdfNamespaces.Office, "office");
                StylesRoot.AppendChild(autoStyles);
            }

            OdfNode? layoutNode = null;
            foreach (var child in autoStyles.Children)
            {
                if (child.LocalName == "page-layout" && child.NamespaceUri == OdfNamespaces.Style)
                {
                    layoutNode = child;
                    break;
                }
            }

            if (layoutNode == null)
            {
                layoutNode = new OdfNode(OdfNodeType.Element, "page-layout", OdfNamespaces.Style, "style");
                layoutNode.SetAttribute("name", OdfNamespaces.Style, "PM1", "style");
                autoStyles.AppendChild(layoutNode);
            }

            var props = FindChildElement(layoutNode, "page-layout-properties", OdfNamespaces.Style);
            if (props == null)
            {
                props = new OdfNode(OdfNodeType.Element, "page-layout-properties", OdfNamespaces.Style, "style");
                layoutNode.AppendChild(props);
            }

            return props;
        }

        protected override string GetDefaultContentXml()
        {
            return "<office:document-content " +
                "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
                "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
                "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
                "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" " +
                "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
                "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" " +
                "xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
                "xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" " +
                "xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" " +
                "xmlns:anim=\"urn:oasis:names:tc:opendocument:xmlns:animation:1.0\" " +
                "xmlns:smil=\"urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0\" " +
                "office:version=\"1.3\">" +
                "<office:body>" +
                "<office:presentation />" +
                "</office:body>" +
                "</office:document-content>";
        }

        protected override string GetDefaultStylesXml()
        {
            return "<office:document-styles " +
                "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
                "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
                "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" " +
                "xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
                "xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" " +
                "xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" " +
                "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
                "office:version=\"1.3\">" +
                "<office:styles></office:styles>" +
                "<office:automatic-styles>" +
                "<style:page-layout style:name=\"PM1\">" +
                "<style:page-layout-properties fo:page-width=\"28cm\" fo:page-height=\"21cm\" style:print-orientation=\"landscape\"/>" +
                "</style:page-layout>" +
                "</office:automatic-styles>" +
                "<office:master-styles>" +
                "<style:master-page style:name=\"Default\" style:page-layout-name=\"PM1\"/>" +
                "</office:master-styles>" +
                "</office:document-styles>";
        }

        protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
        {
            var srcPres = sourceDoc as PresentationDocument ?? throw new ArgumentException("Source document must be a PresentationDocument.");
            var destPresNode = GetPresentationNode();
            var srcPresNode = srcPres.GetPresentationNode();
            
            foreach (var child in srcPresNode.Children)
            {
                if (child.NodeType == OdfNodeType.Element)
                {
                    var imported = OdfNode.ImportNode(child, srcPres.Package, Package);
                    RemapStylesInNodes(imported, renameMap);
                    destPresNode.AppendChild(imported);
                }
            }
            ParseSlides();
        }

        public OdfNode? FindChildElement(OdfNode parent, string localName, string nsUri)
        {
            foreach (var child in parent.Children)
            {
                if (child.NodeType == OdfNodeType.Element && 
                    string.Equals(child.LocalName, localName, StringComparison.Ordinal) && 
                    string.Equals(child.NamespaceUri, nsUri, StringComparison.Ordinal))
                {
                    return child;
                }
            }
            return null;
        }

        public void AddMasterPage(string name, string pageLayoutName)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Master page name cannot be null or empty.", nameof(name));
            if (string.IsNullOrEmpty(pageLayoutName)) throw new ArgumentException("Page layout name cannot be null or empty.", nameof(pageLayoutName));

            var masterStyles = FindChildElement(StylesRoot, "master-styles", OdfNamespaces.Office);
            if (masterStyles == null)
            {
                masterStyles = new OdfNode(OdfNodeType.Element, "master-styles", OdfNamespaces.Office, "office");
                StylesRoot.AppendChild(masterStyles);
            }

            var masterPage = new OdfNode(OdfNodeType.Element, "master-page", OdfNamespaces.Style, "style");
            masterPage.SetAttribute("name", OdfNamespaces.Style, name, "style");
            masterPage.SetAttribute("page-layout-name", OdfNamespaces.Style, pageLayoutName, "style");

            masterStyles.AppendChild(masterPage);
        }

        public OdfPresentationPageLayout CreatePresentationPageLayout(string name)
        {
            var autoStyles = FindChildElement(StylesRoot, "automatic-styles", OdfNamespaces.Office);
            if (autoStyles == null)
            {
                autoStyles = new OdfNode(OdfNodeType.Element, "automatic-styles", OdfNamespaces.Office, "office");
                StylesRoot.AppendChild(autoStyles);
            }
            var layoutNode = new OdfNode(OdfNodeType.Element, "presentation-page-layout", OdfNamespaces.Style, "style");
            layoutNode.SetAttribute("name", OdfNamespaces.Style, name, "style");
            autoStyles.AppendChild(layoutNode);
            return new OdfPresentationPageLayout(layoutNode);
        }

        public OdfPresentationPageLayout? GetPresentationPageLayout(string name)
        {
            // Search in ContentDom first
            var autoStyles = FindChildElement(ContentRoot, "automatic-styles", OdfNamespaces.Office);
            if (autoStyles != null)
            {
                foreach (var child in autoStyles.Children)
                {
                    if (child.LocalName == "presentation-page-layout" && 
                        child.NamespaceUri == OdfNamespaces.Style && 
                        child.GetAttribute("name", OdfNamespaces.Style) == name)
                    {
                        return new OdfPresentationPageLayout(child);
                    }
                }
            }
            // Search in StylesDom
            autoStyles = FindChildElement(StylesRoot, "automatic-styles", OdfNamespaces.Office);
            if (autoStyles != null)
            {
                foreach (var child in autoStyles.Children)
                {
                    if (child.LocalName == "presentation-page-layout" && 
                        child.NamespaceUri == OdfNamespaces.Style && 
                        child.GetAttribute("name", OdfNamespaces.Style) == name)
                    {
                        return new OdfPresentationPageLayout(child);
                    }
                }
            }
            return null;
        }

        public OdfHandoutPage HandoutPage
        {
            get
            {
                var masterStyles = FindChildElement(StylesRoot, "master-styles", OdfNamespaces.Office);
                if (masterStyles == null)
                {
                    masterStyles = new OdfNode(OdfNodeType.Element, "master-styles", OdfNamespaces.Office, "office");
                    StylesRoot.AppendChild(masterStyles);
                }

                var handoutNode = FindChildElement(masterStyles, "handout", OdfNamespaces.Presentation);
                if (handoutNode == null)
                {
                    handoutNode = new OdfNode(OdfNodeType.Element, "handout", OdfNamespaces.Presentation, "presentation");
                    handoutNode.SetAttribute("name", OdfNamespaces.Style, "DefaultHandout", "style");
                    handoutNode.SetAttribute("page-layout-name", OdfNamespaces.Style, "PM1", "style");
                    masterStyles.AppendChild(handoutNode);
                }
                return new OdfHandoutPage(handoutNode, this);
            }
        }
    }

    public class OdfSlide
    {
        public OdfNode Node { get; }
        public PresentationDocument Document { get; }

        public string Name
        {
            get => Node.GetAttribute("name", OdfNamespaces.Draw) ?? string.Empty;
            set => Node.SetAttribute("name", OdfNamespaces.Draw, value, "draw");
        }

        public string MasterPageName
        {
            get => Node.GetAttribute("master-page-name", OdfNamespaces.Draw) ?? string.Empty;
            set => Node.SetAttribute("master-page-name", OdfNamespaces.Draw, value, "draw");
        }

        public string? PresentationPageLayoutName
        {
            get => Node.GetAttribute("presentation-page-layout-name", OdfNamespaces.Presentation);
            set
            {
                if (value == null)
                    Node.RemoveAttribute("presentation-page-layout-name", OdfNamespaces.Presentation);
                else
                    Node.SetAttribute("presentation-page-layout-name", OdfNamespaces.Presentation, value, "presentation");
            }
        }

        public string? UseHeaderName
        {
            get => Node.GetAttribute("use-header-name", OdfNamespaces.Presentation);
            set
            {
                if (value == null)
                    Node.RemoveAttribute("use-header-name", OdfNamespaces.Presentation);
                else
                    Node.SetAttribute("use-header-name", OdfNamespaces.Presentation, value, "presentation");
            }
        }

        public string? UseFooterName
        {
            get => Node.GetAttribute("use-footer-name", OdfNamespaces.Presentation);
            set
            {
                if (value == null)
                    Node.RemoveAttribute("use-footer-name", OdfNamespaces.Presentation);
                else
                    Node.SetAttribute("use-footer-name", OdfNamespaces.Presentation, value, "presentation");
            }
        }

        public string? UseDateTimeName
        {
            get => Node.GetAttribute("use-date-time-name", OdfNamespaces.Presentation);
            set
            {
                if (value == null)
                    Node.RemoveAttribute("use-date-time-name", OdfNamespaces.Presentation);
                else
                    Node.SetAttribute("use-date-time-name", OdfNamespaces.Presentation, value, "presentation");
            }
        }

        public OdfNotesPage SpeakerNotesPage
        {
            get
            {
                var notesNode = Node.FindChildElement("notes", OdfNamespaces.Presentation);
                if (notesNode == null)
                {
                    notesNode = new OdfNode(OdfNodeType.Element, "notes", OdfNamespaces.Presentation, "presentation");
                    Node.AppendChild(notesNode);
                }
                return new OdfNotesPage(notesNode, this);
            }
        }

        public string SpeakerNotes
        {
            get => SpeakerNotesPage.SpeakerNotesText;
            set => SpeakerNotesPage.SpeakerNotesText = value;
        }

        public OdfAnimationNode AnimationRoot
        {
            get
            {
                OdfNode? mainSeq = null;
                foreach (var child in Node.Children)
                {
                    if (child.NodeType == OdfNodeType.Element && child.LocalName == "seq" && child.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:animation:1.0")
                    {
                        string? nodeType = child.GetAttribute("node-type", OdfNamespaces.Presentation);
                        if (nodeType == "main-sequence")
                        {
                            mainSeq = child;
                            break;
                        }
                    }
                }
                if (mainSeq == null)
                {
                    mainSeq = new OdfNode(OdfNodeType.Element, "seq", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
                    mainSeq.SetAttribute("node-type", OdfNamespaces.Presentation, "main-sequence", "presentation");
                    Node.AppendChild(mainSeq);
                }
                return new OdfAnimationNode(mainSeq);
            }
        }

        public IReadOnlyList<OdfPlaceholder> Placeholders
        {
            get
            {
                var list = new List<OdfPlaceholder>();
                foreach (var child in Node.Children)
                {
                    if (child.NodeType == OdfNodeType.Element && child.NamespaceUri == OdfNamespaces.Draw)
                    {
                        string? ph = child.GetAttribute("placeholder", OdfNamespaces.Presentation);
                        if (ph == "true")
                        {
                            list.Add(new OdfPlaceholder(child, this));
                        }
                    }
                }
                return list.AsReadOnly();
            }
        }

        public OdfPlaceholder AddPlaceholder(OdfPlaceholderType type, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
        {
            var shapeNode = new OdfNode(OdfNodeType.Element, "rect", OdfNamespaces.Draw, "draw");
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

        public OdfShape AddEmbeddedObject(string subPath, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
        {
            var frame = CreateDrawingFrame(x, y, w, h);
            var objNode = new OdfNode(OdfNodeType.Element, "object", OdfNamespaces.Draw, "draw");
            
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

        public OdfSlide(OdfNode node, PresentationDocument doc)
        {
            Node = node;
            Document = doc;
        }

        public OdfTextBox AddTextBox(OdfLength x, OdfLength y, OdfLength w, OdfLength h, string text)
        {
            var frame = CreateDrawingFrame(x, y, w, h);
            var textBoxNode = new OdfNode(OdfNodeType.Element, "text-box", OdfNamespaces.Draw, "draw");
            frame.AppendChild(textBoxNode);

            var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            pNode.TextContent = text;
            textBoxNode.AppendChild(pNode);

            Node.AppendChild(frame);
            return new OdfTextBox(frame, this);
        }

        public OdfShape AddShape(OdfShapeType shapeType, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
        {
            string localName = shapeType switch
            {
                OdfShapeType.Rectangle => "rect",
                OdfShapeType.Ellipse => "ellipse",
                _ => "custom-shape"
            };

            var shapeNode = new OdfNode(OdfNodeType.Element, localName, OdfNamespaces.Draw, "draw");
            shapeNode.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
            shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
            shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
            shapeNode.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
            shapeNode.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");

            Node.AppendChild(shapeNode);
            return new OdfShape(shapeNode, this);
        }

        public OdfShape AddPolyline(IEnumerable<System.Drawing.PointF> points, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
        {
            var shapeNode = new OdfNode(OdfNodeType.Element, "polyline", OdfNamespaces.Draw, "draw");
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

        public OdfPicture AddPicture(byte[] imageBytes, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
        {
            var frame = CreateDrawingFrame(x, y, w, h);
            
            var mediaManager = new OdfMediaManager(Document.Package);
            string imageHref = mediaManager.AddImage(imageBytes, "slide_image.png");

            var imgNode = new OdfNode(OdfNodeType.Element, "image", OdfNamespaces.Draw, "draw");
            imgNode.SetAttribute("href", OdfNamespaces.XLink, imageHref, "xlink");
            imgNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
            imgNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
            imgNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");
            
            frame.AppendChild(imgNode);
            Node.AppendChild(frame);
            return new OdfPicture(frame, this);
        }

        public void SetTransition(OdfTransitionType type, OdfLength duration)
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
        }

        private OdfNode CreateDrawingFrame(OdfLength x, OdfLength y, OdfLength w, OdfLength h)
        {
            var frame = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
            frame.SetAttribute("id", OdfNamespaces.Draw, "frm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
            frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
            frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
            frame.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
            frame.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
            frame.SetAttribute("anchor-type", OdfNamespaces.Draw, "page", "draw");
            return frame;
        }

        private OdfNode? FindTextBoxInNotes(OdfNode notesNode)
        {
            foreach (var frame in notesNode.Children)
            {
                if (frame.NodeType == OdfNodeType.Element && frame.LocalName == "frame" && frame.NamespaceUri == OdfNamespaces.Draw)
                {
                    string? cls = frame.GetAttribute("class", OdfNamespaces.Presentation);
                    if (cls == "notes")
                    {
                        return Document.FindChildElement(frame, "text-box", OdfNamespaces.Draw);
                    }
                }
            }
            return null;
        }
    }

    public class OdfShape
    {
        public OdfNode Node { get; }
        public OdfSlide? Slide { get; }
        public OdfDocument Document { get; }

        public string Id
        {
            get => Node.GetAttribute("id", OdfNamespaces.Draw) ?? string.Empty;
            set => Node.SetAttribute("id", OdfNamespaces.Draw, value, "draw");
        }

        public string? FillColor
        {
            get => Document.StyleEngine.GetStyleProperty(Node.GetAttribute("style-name", OdfNamespaces.Draw) ?? string.Empty, "fill-color", OdfNamespaces.Draw, "graphic");
            set
            {
                Document.StyleEngine.SetLocalStyleProperty(Node, "graphic", "graphic-properties", "fill", OdfNamespaces.Draw, "solid", "draw");
                Document.StyleEngine.SetLocalStyleProperty(Node, "graphic", "graphic-properties", "fill-color", OdfNamespaces.Draw, value ?? string.Empty, "draw");
            }
        }

        public string? StrokeColor
        {
            get => Document.StyleEngine.GetStyleProperty(Node.GetAttribute("style-name", OdfNamespaces.Draw) ?? string.Empty, "stroke-color", OdfNamespaces.Svg, "graphic");
            set
            {
                Document.StyleEngine.SetLocalStyleProperty(Node, "graphic", "graphic-properties", "stroke", OdfNamespaces.Draw, "solid", "draw");
                Document.StyleEngine.SetLocalStyleProperty(Node, "graphic", "graphic-properties", "stroke-color", OdfNamespaces.Svg, value ?? string.Empty, "svg");
            }
        }

        public OdfShape(OdfNode node, OdfSlide slide)
        {
            Node = node;
            Slide = slide;
            Document = slide?.Document!;
        }

        public OdfShape(OdfNode node, OdfDocument doc)
        {
            Node = node;
            Slide = null;
            Document = doc;
        }

        public void Animate(OdfAnimationType type, OdfLength duration, OdfLength delay)
        {
            if (Slide == null)
            {
                throw new InvalidOperationException("Animation is only supported for presentation slides.");
            }
            var slideNode = Slide.Node;
            var mainSeq = FindOrCreateAnimationSequence(slideNode);

            var stepPar = new OdfNode(OdfNodeType.Element, "par", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
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
                        var filter = new OdfNode(OdfNodeType.Element, "transitionFilter", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
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
                        var filter = new OdfNode(OdfNodeType.Element, "transitionFilter", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
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
                        var filter = new OdfNode(OdfNodeType.Element, "transitionFilter", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
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
                        var filter = new OdfNode(OdfNodeType.Element, "transitionFilter", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
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

        private OdfNode FindOrCreateAnimationSequence(OdfNode slideNode)
        {
            foreach (var child in slideNode.Children)
            {
                if (child.NodeType == OdfNodeType.Element && child.LocalName == "seq" && child.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:animation:1.0")
                {
                    string? nodeType = child.GetAttribute("node-type", OdfNamespaces.Presentation);
                    if (nodeType == "main-sequence") return child;
                }
            }

            var mainSeq = new OdfNode(OdfNodeType.Element, "seq", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
            mainSeq.SetAttribute("node-type", OdfNamespaces.Presentation, "main-sequence", "presentation");
            slideNode.AppendChild(mainSeq);
            return mainSeq;
        }
    }

    public class OdfTextBox : OdfShape
    {
        public OdfTextBox(OdfNode node, OdfSlide slide) : base(node, slide) { }
        public OdfTextBox(OdfNode node, OdfDocument doc) : base(node, doc) { }
    }

    public class OdfPicture : OdfShape
    {
        public OdfPicture(OdfNode node, OdfSlide slide) : base(node, slide) { }
        public OdfPicture(OdfNode node, OdfDocument doc) : base(node, doc) { }
    }
}
