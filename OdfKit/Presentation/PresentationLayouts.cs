#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Core;
using OdfNamespaces = OdfKit.Core.OdfNamespaces;

namespace OdfKit.Presentation
{
    public enum OdfPlaceholderType
    {
        Title,
        Subtitle,
        Outline,
        Text,
        Graphic,
        Object,
        Chart,
        Table,
        Orgchart,
        PageNumber,
        Header,
        Footer,
        DateTime,
        Notes,
        Handout
    }

    public class OdfPlaceholderTemplate
    {
        public OdfNode Node { get; }

        public OdfPlaceholderType PlaceholderType
        {
            get => KebabToType(Node.GetAttribute("class", OdfNamespaces.Presentation) ?? "text");
            set => Node.SetAttribute("class", OdfNamespaces.Presentation, TypeToKebab(value), "presentation");
        }

        public OdfLength? X
        {
            get
            {
                string? val = Node.GetAttribute("x", OdfNamespaces.Svg);
                return val != null ? OdfLength.Parse(val) : (OdfLength?)null;
            }
            set => Node.SetAttribute("x", OdfNamespaces.Svg, value?.ToString() ?? string.Empty, "svg");
        }

        public OdfLength? Y
        {
            get
            {
                string? val = Node.GetAttribute("y", OdfNamespaces.Svg);
                return val != null ? OdfLength.Parse(val) : (OdfLength?)null;
            }
            set => Node.SetAttribute("y", OdfNamespaces.Svg, value?.ToString() ?? string.Empty, "svg");
        }

        public OdfLength? Width
        {
            get
            {
                string? val = Node.GetAttribute("width", OdfNamespaces.Svg);
                return val != null ? OdfLength.Parse(val) : (OdfLength?)null;
            }
            set => Node.SetAttribute("width", OdfNamespaces.Svg, value?.ToString() ?? string.Empty, "svg");
        }

        public OdfLength? Height
        {
            get
            {
                string? val = Node.GetAttribute("height", OdfNamespaces.Svg);
                return val != null ? OdfLength.Parse(val) : (OdfLength?)null;
            }
            set => Node.SetAttribute("height", OdfNamespaces.Svg, value?.ToString() ?? string.Empty, "svg");
        }


        public OdfPlaceholderTemplate(OdfNode node)
        {
            Node = node;
        }

        internal static string TypeToKebab(OdfPlaceholderType type)
        {
            return type switch
            {
                OdfPlaceholderType.Title => "title",
                OdfPlaceholderType.Subtitle => "subtitle",
                OdfPlaceholderType.Outline => "outline",
                OdfPlaceholderType.Text => "text",
                OdfPlaceholderType.Graphic => "graphic",
                OdfPlaceholderType.Object => "object",
                OdfPlaceholderType.Chart => "chart",
                OdfPlaceholderType.Table => "table",
                OdfPlaceholderType.Orgchart => "orgchart",
                OdfPlaceholderType.PageNumber => "page-number",
                OdfPlaceholderType.Header => "header",
                OdfPlaceholderType.Footer => "footer",
                OdfPlaceholderType.DateTime => "date-time",
                OdfPlaceholderType.Notes => "notes",
                OdfPlaceholderType.Handout => "handout",
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
        }

        internal static OdfPlaceholderType KebabToType(string kebab)
        {
            return kebab switch
            {
                "title" => OdfPlaceholderType.Title,
                "subtitle" => OdfPlaceholderType.Subtitle,
                "outline" => OdfPlaceholderType.Outline,
                "text" => OdfPlaceholderType.Text,
                "graphic" => OdfPlaceholderType.Graphic,
                "object" => OdfPlaceholderType.Object,
                "chart" => OdfPlaceholderType.Chart,
                "table" => OdfPlaceholderType.Table,
                "orgchart" => OdfPlaceholderType.Orgchart,
                "page-number" => OdfPlaceholderType.PageNumber,
                "header" => OdfPlaceholderType.Header,
                "footer" => OdfPlaceholderType.Footer,
                "date-time" => OdfPlaceholderType.DateTime,
                "notes" => OdfPlaceholderType.Notes,
                "handout" => OdfPlaceholderType.Handout,
                _ => OdfPlaceholderType.Text
            };
        }
    }

    public class OdfPresentationPageLayout
    {
        public OdfNode Node { get; }

        public string Name
        {
            get => Node.GetAttribute("name", OdfNamespaces.Style) ?? string.Empty;
            set => Node.SetAttribute("name", OdfNamespaces.Style, value, "style");
        }

        public IReadOnlyList<OdfPlaceholderTemplate> Placeholders
        {
            get
            {
                var list = new List<OdfPlaceholderTemplate>();
                foreach (var child in Node.Children)
                {
                    if (child.LocalName == "placeholder" && child.NamespaceUri == OdfNamespaces.Presentation)
                    {
                        list.Add(new OdfPlaceholderTemplate(child));
                    }
                }
                return list.AsReadOnly();
            }
        }

        public OdfPresentationPageLayout(OdfNode node)
        {
            Node = node;
        }

        public OdfPlaceholderTemplate AddPlaceholder(OdfPlaceholderType type, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
        {
            var phNode = new OdfNode(OdfNodeType.Element, "placeholder", OdfNamespaces.Presentation, "presentation");
            phNode.SetAttribute("class", OdfNamespaces.Presentation, OdfPlaceholderTemplate.TypeToKebab(type), "presentation");
            phNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
            phNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
            phNode.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
            phNode.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
            Node.AppendChild(phNode);
            return new OdfPlaceholderTemplate(phNode);
        }

        public void RemovePlaceholder(OdfPlaceholderType type)
        {
            string clsVal = OdfPlaceholderTemplate.TypeToKebab(type);
            var toRemove = new List<OdfNode>();
            foreach (var child in Node.Children)
            {
                if (child.LocalName == "placeholder" && child.NamespaceUri == OdfNamespaces.Presentation)
                {
                    if (child.GetAttribute("class", OdfNamespaces.Presentation) == clsVal)
                    {
                        toRemove.Add(child);
                    }
                }
            }
            foreach (var child in toRemove)
            {
                Node.RemoveChild(child);
            }
        }
    }

    public class OdfPlaceholder : OdfShape
    {
        public OdfPlaceholderType PlaceholderType
        {
            get => OdfPlaceholderTemplate.KebabToType(Node.GetAttribute("class", OdfNamespaces.Presentation) ?? "text");
            set => Node.SetAttribute("class", OdfNamespaces.Presentation, OdfPlaceholderTemplate.TypeToKebab(value), "presentation");
        }

        public OdfPlaceholder(OdfNode node, OdfSlide slide) : base(node, slide)
        {
            Node.SetAttribute("placeholder", OdfNamespaces.Presentation, "true", "presentation");
        }
    }
}
