using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text
{
    /// <summary>
    /// Represents a ruby layout element used for East Asian annotations.
    /// </summary>
    public class OdfRuby
    {
        /// <summary>
        /// Gets the underlying ODF node representing the ruby element.
        /// </summary>
        public OdfNode Node { get; }
        private readonly TextDocument _doc;

        /// <summary>
        /// Initializes a new instance of the <see cref="OdfRuby"/> class.
        /// </summary>
        public OdfRuby(OdfNode node, TextDocument doc)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        /// <summary>
        /// Gets or sets the style name associated with the ruby element.
        /// </summary>
        public string? StyleName
        {
            get => Node.GetAttribute("style-name", OdfNamespaces.Text);
            set => Node.SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, "text");
        }

        /// <summary>
        /// Gets the ruby base node.
        /// </summary>
        public OdfNode? RubyBaseNode
        {
            get
            {
                foreach (var child in Node.Children)
                {
                    if (child.LocalName == "ruby-base" && child.NamespaceUri == OdfNamespaces.Text)
                    {
                        return child;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Gets the ruby text node.
        /// </summary>
        public OdfNode? RubyTextNode
        {
            get
            {
                foreach (var child in Node.Children)
                {
                    if (child.LocalName == "ruby-text" && child.NamespaceUri == OdfNamespaces.Text)
                    {
                        return child;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Gets or sets the style name associated with the ruby base element.
        /// </summary>
        public string? RubyBaseStyleName
        {
            get => RubyBaseNode?.GetAttribute("style-name", OdfNamespaces.Text);
            set => RubyBaseNode?.SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, "text");
        }

        /// <summary>
        /// Gets or sets the style name associated with the ruby text element.
        /// </summary>
        public string? RubyTextStyleName
        {
            get => RubyTextNode?.GetAttribute("style-name", OdfNamespaces.Text);
            set => RubyTextNode?.SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, "text");
        }

        /// <summary>
        /// Gets or sets the ruby position (e.g., "above", "below").
        /// </summary>
        public string? RubyPosition
        {
            get => _doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "ruby-position", OdfNamespaces.Style, "ruby");
            set => _doc.StyleEngine.SetLocalStyleProperty(Node, "ruby", "ruby-properties", "ruby-position", OdfNamespaces.Style, value ?? string.Empty, "style");
        }

        /// <summary>
        /// Gets or sets the ruby alignment (e.g., "left", "center", "right", "distribute-letter", "distribute-space").
        /// </summary>
        public string? RubyAlign
        {
            get => _doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "ruby-align", OdfNamespaces.Style, "ruby");
            set => _doc.StyleEngine.SetLocalStyleProperty(Node, "ruby", "ruby-properties", "ruby-align", OdfNamespaces.Style, value ?? string.Empty, "style");
        }
    }
}
