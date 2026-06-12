using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Presentation;

namespace OdfKit.Drawing
{
    public class DrawingDocument : OdfDocument
    {
        private readonly List<OdfDrawPage> _pages = new();

        public IReadOnlyList<OdfDrawPage> Pages => _pages.AsReadOnly();

        public DrawingDocument(OdfPackage package) : base(package)
        {
            if (string.IsNullOrEmpty(package.MimeType))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.graphics");
            }
            ParsePages();
        }

        private void ParsePages()
        {
            _pages.Clear();
            var drawingNode = GetDrawingNode();
            foreach (var child in drawingNode.Children)
            {
                if (child.NodeType == OdfNodeType.Element && child.LocalName == "page" && child.NamespaceUri == OdfNamespaces.Draw)
                {
                    _pages.Add(new OdfDrawPage(child, this));
                }
            }
        }

        public OdfNode GetDrawingNode()
        {
            var body = FindChildElement(ContentRoot, "body", OdfNamespaces.Office);
            if (body == null)
            {
                body = new OdfNode(OdfNodeType.Element, "body", OdfNamespaces.Office, "office");
                ContentRoot.AppendChild(body);
            }

            var drawing = FindChildElement(body, "drawing", OdfNamespaces.Office);
            if (drawing == null)
            {
                drawing = new OdfNode(OdfNodeType.Element, "drawing", OdfNamespaces.Office, "office");
                body.AppendChild(drawing);
            }

            return drawing;
        }

        public OdfDrawPage AddPage(string? name = null)
        {
            var drawingNode = GetDrawingNode();
            var pageNode = OdfNodeFactory.CreateElement("page", OdfNamespaces.Draw, "draw");
            
            string pageName = name ?? $"Page {_pages.Count + 1}";
            pageNode.SetAttribute("name", OdfNamespaces.Draw, pageName, "draw");

            drawingNode.AppendChild(pageNode);
            var page = new OdfDrawPage(pageNode, this);
            _pages.Add(page);
            return page;
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
                "xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" " +
                "office:version=\"1.3\">" +
                "<office:body>" +
                "<office:drawing />" +
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
                "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
                "office:version=\"1.3\">" +
                "<office:styles></office:styles>" +
                "<office:automatic-styles></office:automatic-styles>" +
                "<office:master-styles></office:master-styles>" +
                "</office:document-styles>";
        }

        protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
        {
            var srcDraw = sourceDoc as DrawingDocument ?? throw new ArgumentException("Source document must be a DrawingDocument.");
            var destDrawNode = GetDrawingNode();
            var srcDrawNode = srcDraw.GetDrawingNode();
            
            foreach (var child in srcDrawNode.Children)
            {
                if (child.NodeType == OdfNodeType.Element)
                {
                    var imported = OdfNode.ImportNode(child, srcDraw.Package, Package);
                    RemapStylesInNodes(imported, renameMap);
                    destDrawNode.AppendChild(imported);
                }
            }
            ParsePages();
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
    }

    public class OdfDrawPage
    {
        public OdfNode Node { get; }
        public DrawingDocument Document { get; }

        public OdfDrawPage(OdfNode node, DrawingDocument doc)
        {
            Node = node;
            Document = doc;
        }

        public string Name
        {
            get => Node.GetAttribute("name", OdfNamespaces.Draw) ?? string.Empty;
            set => Node.SetAttribute("name", OdfNamespaces.Draw, value, "draw");
        }

        public string? MasterPageName
        {
            get => Node.GetAttribute("master-page-name", OdfNamespaces.Draw);
            set => Node.SetAttribute("master-page-name", OdfNamespaces.Draw, value ?? string.Empty, "draw");
        }

        public OdfTextBox AddTextBox(OdfLength x, OdfLength y, OdfLength w, OdfLength h, string text)
        {
            var frame = CreateDrawingFrame(x, y, w, h);
            var textBoxNode = OdfNodeFactory.CreateElement("text-box", OdfNamespaces.Draw, "draw");
            frame.AppendChild(textBoxNode);

            var pNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
            pNode.TextContent = text;
            textBoxNode.AppendChild(pNode);

            Node.AppendChild(frame);
            return new OdfTextBox(frame, Document);
        }

        public OdfShape AddShape(OdfShapeType shapeType, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
        {
            string localName = shapeType switch
            {
                OdfShapeType.Rectangle => "rect",
                OdfShapeType.Ellipse => "ellipse",
                _ => "custom-shape"
            };

            var shapeNode = OdfNodeFactory.CreateElement(localName, OdfNamespaces.Draw, "draw");
            shapeNode.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
            shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
            shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
            shapeNode.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
            shapeNode.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");

            Node.AppendChild(shapeNode);
            return new OdfShape(shapeNode, Document);
        }

        public OdfShape AddPolyline(IEnumerable<System.Drawing.PointF> points, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
        {
            var shapeNode = OdfNodeFactory.CreateElement("polyline", OdfNamespaces.Draw, "draw");
            shapeNode.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
            shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
            shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
            shapeNode.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
            shapeNode.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");

            var pointsStr = string.Join(" ", points.Select(p => $"{p.X.ToString(System.Globalization.CultureInfo.InvariantCulture)},{p.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
            shapeNode.SetAttribute("points", OdfNamespaces.Draw, pointsStr, "draw");

            Node.AppendChild(shapeNode);
            return new OdfShape(shapeNode, Document);
        }

        private OdfNode CreateDrawingFrame(OdfLength x, OdfLength y, OdfLength w, OdfLength h)
        {
            var frame = OdfNodeFactory.CreateElement("frame", OdfNamespaces.Draw, "draw");
            frame.SetAttribute("id", OdfNamespaces.Draw, "frm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
            frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
            frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
            frame.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
            frame.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
            return frame;
        }
    }
}
