using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Core;
using OdfNamespaces = OdfKit.Core.OdfNamespaces;

namespace OdfKit.Presentation
{
    public class OdfNotesPage
    {
        public OdfNode Node { get; }
        public OdfSlide Slide { get; }

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
                if (textBox == null)
                {
                    var frame = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
                    frame.SetAttribute("class", OdfNamespaces.Presentation, "notes", "presentation");
                    frame.SetAttribute("x", OdfNamespaces.Svg, "2cm", "svg");
                    frame.SetAttribute("y", OdfNamespaces.Svg, "15cm", "svg");
                    frame.SetAttribute("width", OdfNamespaces.Svg, "20cm", "svg");
                    frame.SetAttribute("height", OdfNamespaces.Svg, "10cm", "svg");

                    var box = new OdfNode(OdfNodeType.Element, "text-box", OdfNamespaces.Draw, "draw");
                    frame.AppendChild(box);
                    Node.AppendChild(frame);
                    textBox = box;
                }

                textBox.Children.Clear();
                var p = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
                p.TextContent = value;
                textBox.AppendChild(p);
            }
        }

        public string? MasterPageName
        {
            get => Node.GetAttribute("master-page-name", OdfNamespaces.Draw);
            set
            {
                if (value == null)
                    Node.RemoveAttribute("master-page-name", OdfNamespaces.Draw);
                else
                    Node.SetAttribute("master-page-name", OdfNamespaces.Draw, value, "draw");
            }
        }

        public IReadOnlyList<OdfShape> Shapes
        {
            get
            {
                var list = new List<OdfShape>();
                foreach (var child in Node.Children)
                {
                    if (child.NodeType == OdfNodeType.Element && child.NamespaceUri == OdfNamespaces.Draw && child.LocalName != "page-thumbnail")
                    {
                        if (child.LocalName == "frame" && child.FindChildElement("text-box", OdfNamespaces.Draw) != null)
                        {
                            list.Add(new OdfTextBox(child, Slide));
                        }
                        else if (child.LocalName == "frame" && child.FindChildElement("image", OdfNamespaces.Draw) != null)
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

        public OdfNotesPage(OdfNode node, OdfSlide slide)
        {
            Node = node;
            Slide = slide;
        }

        public OdfTextBox AddTextBox(OdfLength x, OdfLength y, OdfLength w, OdfLength h, string text)
        {
            var frame = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
            frame.SetAttribute("id", OdfNamespaces.Draw, "frm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
            frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
            frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
            frame.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
            frame.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
            frame.SetAttribute("anchor-type", OdfNamespaces.Draw, "page", "draw");

            var textBoxNode = new OdfNode(OdfNodeType.Element, "text-box", OdfNamespaces.Draw, "draw");
            frame.AppendChild(textBoxNode);

            var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            pNode.TextContent = text;
            textBoxNode.AppendChild(pNode);

            Node.AppendChild(frame);
            return new OdfTextBox(frame, Slide);
        }

        public void AddSlideThumbnail(OdfLength x, OdfLength y, OdfLength w, OdfLength h)
        {
            var thumbnail = new OdfNode(OdfNodeType.Element, "page-thumbnail", OdfNamespaces.Draw, "draw");
            thumbnail.SetAttribute("id", OdfNamespaces.Draw, "thm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
            thumbnail.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
            thumbnail.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
            thumbnail.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
            thumbnail.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
            thumbnail.SetAttribute("page-number", OdfNamespaces.Draw, Slide.Name, "draw");
            Node.AppendChild(thumbnail);
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
            return new OdfShape(shapeNode, Slide.Document);
        }

        public OdfPicture AddPicture(byte[] imageBytes, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
        {
            var frame = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
            frame.SetAttribute("id", OdfNamespaces.Draw, "frm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
            frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
            frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
            frame.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
            frame.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
            frame.SetAttribute("anchor-type", OdfNamespaces.Draw, "page", "draw");

            var mediaManager = new OdfMediaManager(Slide.Document.Package);
            string imageHref = mediaManager.AddImage(imageBytes, "notes_image.png");

            var imgNode = new OdfNode(OdfNodeType.Element, "image", OdfNamespaces.Draw, "draw");
            imgNode.SetAttribute("href", OdfNamespaces.XLink, imageHref, "xlink");
            imgNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
            imgNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
            imgNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");

            frame.AppendChild(imgNode);
            Node.AppendChild(frame);
            return new OdfPicture(frame, Slide.Document);
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
                        foreach (var child in frame.Children)
                        {
                            if (child.NodeType == OdfNodeType.Element && child.LocalName == "text-box" && child.NamespaceUri == OdfNamespaces.Draw)
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

    public class OdfHandoutPage
    {
        public OdfNode Node { get; }
        public PresentationDocument Document { get; }

        public string? Name
        {
            get => Node.GetAttribute("name", OdfNamespaces.Style);
            set
            {
                if (value == null)
                    Node.RemoveAttribute("name", OdfNamespaces.Style);
                else
                    Node.SetAttribute("name", OdfNamespaces.Style, value, "style");
            }
        }

        public string? MasterPageName
        {
            get => Node.GetAttribute("master-page-name", OdfNamespaces.Draw);
            set
            {
                if (value == null)
                    Node.RemoveAttribute("master-page-name", OdfNamespaces.Draw);
                else
                    Node.SetAttribute("master-page-name", OdfNamespaces.Draw, value, "draw");
            }
        }

        public IReadOnlyList<OdfShape> Shapes
        {
            get
            {
                var list = new List<OdfShape>();
                foreach (var child in Node.Children)
                {
                    if (child.NodeType == OdfNodeType.Element && child.NamespaceUri == OdfNamespaces.Draw && child.LocalName != "page-thumbnail")
                    {
                        if (child.LocalName == "frame" && child.FindChildElement("text-box", OdfNamespaces.Draw) != null)
                        {
                            list.Add(new OdfTextBox(child, Document));
                        }
                        else if (child.LocalName == "frame" && child.FindChildElement("image", OdfNamespaces.Draw) != null)
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

        public OdfHandoutPage(OdfNode node, PresentationDocument doc)
        {
            Node = node;
            Document = doc;
        }

        public OdfTextBox AddTextBox(OdfLength x, OdfLength y, OdfLength w, OdfLength h, string text)
        {
            var frame = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
            frame.SetAttribute("id", OdfNamespaces.Draw, "frm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
            frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
            frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
            frame.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
            frame.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
            frame.SetAttribute("anchor-type", OdfNamespaces.Draw, "page", "draw");

            var textBoxNode = new OdfNode(OdfNodeType.Element, "text-box", OdfNamespaces.Draw, "draw");
            frame.AppendChild(textBoxNode);

            var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            pNode.TextContent = text;
            textBoxNode.AppendChild(pNode);

            Node.AppendChild(frame);
            return new OdfTextBox(frame, Document);
        }

        public void AddSlideThumbnailPlaceholder(OdfLength x, OdfLength y, OdfLength w, OdfLength h)
        {
            var thumbnail = new OdfNode(OdfNodeType.Element, "page-thumbnail", OdfNamespaces.Draw, "draw");
            thumbnail.SetAttribute("id", OdfNamespaces.Draw, "thm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
            thumbnail.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
            thumbnail.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
            thumbnail.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
            thumbnail.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
            Node.AppendChild(thumbnail);
        }
    }
}
