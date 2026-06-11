using System;
using System.IO;
using OdfKit.Core;
using OdfKit.Compliance;

namespace OdfKit.DOM
{
    /// <summary>
    /// Base class for all specialized typed ODF element wrappers.
    /// </summary>
    public class OdfElement : OdfNode
    {
        public OdfElement(string localName, string namespaceUri, string? prefix = null)
            : base(OdfNodeType.Element, localName, namespaceUri, prefix)
        {
        }

        /// <summary>
        /// Gets a schema-defined attribute value with version context.
        /// </summary>
        public string? GetAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
        {
            var attrDef = OdfSchemaRegistry.GetSchema(version).FindAttribute(namespaceUri, localName);
            if (attrDef == null)
            {
                OdfKitDiagnostics.Warn($"Attribute '{localName}' in namespace '{namespaceUri}' is not defined in ODF {version} schema.");
            }
            return GetAttribute(localName, namespaceUri);
        }

        /// <summary>
        /// Sets a schema-defined attribute value with version context.
        /// </summary>
        public void SetAttributeValue(string localName, string namespaceUri, string value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
        {
            var attrDef = OdfSchemaRegistry.GetSchema(version).FindAttribute(namespaceUri, localName);
            if (attrDef == null)
            {
                OdfKitDiagnostics.Warn($"Attribute '{localName}' in namespace '{namespaceUri}' is not defined in ODF {version} schema.");
            }
            SetAttribute(localName, namespaceUri, value, prefix);
        }

        /// <summary>
        /// Clones the current element, returning a new typed element instance.
        /// </summary>
        public override OdfNode CloneNode(bool deep)
        {
            var clone = OdfNodeFactory.CreateElement(LocalName, NamespaceUri, Prefix);
            foreach (var attr in Attributes)
            {
                clone.Attributes[attr.Key] = attr.Value;
            }
            if (deep)
            {
                foreach (var child in Children)
                {
                    clone.AppendChild(child.CloneNode(true));
                }
            }
            return clone;
        }
    }

    #region Text Wrappers

    public partial class TextPElement : OdfElement
    {
        public TextPElement(string? prefix = null) : base("p", OdfNamespaces.Text, prefix) { }

        public string? StyleName
        {
            get => GetAttribute("style-name", OdfNamespaces.Text);
            set => SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        }
    }

    public partial class TextHElement : OdfElement
    {
        public TextHElement(string? prefix = null) : base("h", OdfNamespaces.Text, prefix) { }

        public string? StyleName
        {
            get => GetAttribute("style-name", OdfNamespaces.Text);
            set => SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        }

        public int OutlineLevel
        {
            get => int.TryParse(GetAttribute("outline-level", OdfNamespaces.Text), out var level) ? level : 1;
            set => SetAttribute("outline-level", OdfNamespaces.Text, value.ToString(), OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        }
    }

    public partial class TextSpanElement : OdfElement
    {
        public TextSpanElement(string? prefix = null) : base("span", OdfNamespaces.Text, prefix) { }

        public string? StyleName
        {
            get => GetAttribute("style-name", OdfNamespaces.Text);
            set => SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        }
    }

    public partial class TextListElement : OdfElement
    {
        public TextListElement(string? prefix = null) : base("list", OdfNamespaces.Text, prefix) { }

        public string? StyleName
        {
            get => GetAttribute("style-name", OdfNamespaces.Text);
            set => SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        }
    }

    public partial class TextListItemElement : OdfElement
    {
        public TextListItemElement(string? prefix = null) : base("list-item", OdfNamespaces.Text, prefix) { }
    }

    public partial class TextSectionElement : OdfElement
    {
        public TextSectionElement(string? prefix = null) : base("section", OdfNamespaces.Text, prefix) { }

        public string? Name
        {
            get => GetAttribute("name", OdfNamespaces.Text);
            set => SetAttribute("name", OdfNamespaces.Text, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        }

        public string? StyleName
        {
            get => GetAttribute("style-name", OdfNamespaces.Text);
            set => SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        }
    }

    public partial class TextBookmarkElement : OdfElement
    {
        public TextBookmarkElement(string? prefix = null) : base("bookmark", OdfNamespaces.Text, prefix) { }

        public string? Name
        {
            get => GetAttribute("name", OdfNamespaces.Text);
            set => SetAttribute("name", OdfNamespaces.Text, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        }
    }

    public partial class TextNoteElement : OdfElement
    {
        public TextNoteElement(string? prefix = null) : base("note", OdfNamespaces.Text, prefix) { }

        public string? NoteClass
        {
            get => GetAttribute("note-class", OdfNamespaces.Text);
            set => SetAttribute("note-class", OdfNamespaces.Text, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        }
    }

    public partial class OfficeAnnotationElement : OdfElement
    {
        public OfficeAnnotationElement(string? prefix = null) : base("annotation", OdfNamespaces.Office, prefix) { }

        public string? Name
        {
            get => GetAttribute("name", OdfNamespaces.Office);
            set => SetAttribute("name", OdfNamespaces.Office, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Office));
        }
    }

    #endregion

    #region Table Wrappers

    public partial class TableTableElement : OdfElement
    {
        public TableTableElement(string? prefix = null) : base("table", OdfNamespaces.Table, prefix) { }

        public string? Name
        {
            get => GetAttribute("name", OdfNamespaces.Table);
            set => SetAttribute("name", OdfNamespaces.Table, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        }

        public string? StyleName
        {
            get => GetAttribute("style-name", OdfNamespaces.Table);
            set => SetAttribute("style-name", OdfNamespaces.Table, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        }
    }

    public partial class TableTableRowElement : OdfElement
    {
        public TableTableRowElement(string? prefix = null) : base("table-row", OdfNamespaces.Table, prefix) { }

        public int NumberRowsRepeated
        {
            get => int.TryParse(GetAttribute("number-rows-repeated", OdfNamespaces.Table), out var val) ? val : 1;
            set => SetAttribute("number-rows-repeated", OdfNamespaces.Table, value.ToString(), OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        }
    }

    public partial class TableTableCellElement : OdfElement
    {
        public TableTableCellElement(string? prefix = null) : base("table-cell", OdfNamespaces.Table, prefix) { }

        public int NumberColumnsRepeated
        {
            get => int.TryParse(GetAttribute("number-columns-repeated", OdfNamespaces.Table), out var val) ? val : 1;
            set => SetAttribute("number-columns-repeated", OdfNamespaces.Table, value.ToString(), OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        }

        public string? ValueType
        {
            get => GetAttribute("value-type", OdfNamespaces.Office);
            set => SetAttribute("value-type", OdfNamespaces.Office, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Office));
        }
    }

    public partial class TableCoveredTableCellElement : OdfElement
    {
        public TableCoveredTableCellElement(string? prefix = null) : base("covered-table-cell", OdfNamespaces.Table, prefix) { }
    }

    public partial class TableNamedRangeElement : OdfElement
    {
        public TableNamedRangeElement(string? prefix = null) : base("named-range", OdfNamespaces.Table, prefix) { }

        public string? Name
        {
            get => GetAttribute("name", OdfNamespaces.Table);
            set => SetAttribute("name", OdfNamespaces.Table, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        }
    }

    public partial class TableDatabaseRangeElement : OdfElement
    {
        public TableDatabaseRangeElement(string? prefix = null) : base("database-range", OdfNamespaces.Table, prefix) { }

        public string? Name
        {
            get => GetAttribute("name", OdfNamespaces.Table);
            set => SetAttribute("name", OdfNamespaces.Table, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        }
    }

    #endregion

    #region Draw Wrappers

    public partial class DrawFrameElement : OdfElement
    {
        public DrawFrameElement(string? prefix = null) : base("frame", OdfNamespaces.Draw, prefix) { }

        public string? Name
        {
            get => GetAttribute("name", OdfNamespaces.Draw);
            set => SetAttribute("name", OdfNamespaces.Draw, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        }
    }

    public partial class DrawImageElement : OdfElement
    {
        public DrawImageElement(string? prefix = null) : base("image", OdfNamespaces.Draw, prefix) { }

        public string? Href
        {
            get => GetAttribute("href", OdfNamespaces.XLink);
            set => SetAttribute("href", OdfNamespaces.XLink, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.XLink));
        }
    }

    public partial class DrawObjectElement : OdfElement
    {
        public DrawObjectElement(string? prefix = null) : base("object", OdfNamespaces.Draw, prefix) { }

        public string? Href
        {
            get => GetAttribute("href", OdfNamespaces.XLink);
            set => SetAttribute("href", OdfNamespaces.XLink, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.XLink));
        }
    }

    public partial class DrawShapeElement : OdfElement
    {
        public DrawShapeElement(string shapeKind, string? prefix = null) : base(shapeKind, OdfNamespaces.Draw, prefix) { }

        public string? Name
        {
            get => GetAttribute("name", OdfNamespaces.Draw);
            set => SetAttribute("name", OdfNamespaces.Draw, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        }
    }

    public partial class DrawGroupElement : OdfElement
    {
        public DrawGroupElement(string? prefix = null) : base("g", OdfNamespaces.Draw, prefix) { }
    }

    public partial class DrawConnectorElement : OdfElement
    {
        public DrawConnectorElement(string? prefix = null) : base("connector", OdfNamespaces.Draw, prefix) { }
    }

    #endregion

    #region Style Wrappers

    public partial class StyleStyleElement : OdfElement
    {
        public StyleStyleElement(string? prefix = null) : base("style", OdfNamespaces.Style, prefix) { }

        public string? Name
        {
            get => GetAttribute("name", OdfNamespaces.Style);
            set => SetAttribute("name", OdfNamespaces.Style, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        }

        public string? Family
        {
            get => GetAttribute("family", OdfNamespaces.Style);
            set => SetAttribute("family", OdfNamespaces.Style, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        }
    }

    public partial class StyleDefaultStyleElement : OdfElement
    {
        public StyleDefaultStyleElement(string? prefix = null) : base("default-style", OdfNamespaces.Style, prefix) { }

        public string? Family
        {
            get => GetAttribute("family", OdfNamespaces.Style);
            set => SetAttribute("family", OdfNamespaces.Style, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        }
    }

    public partial class StyleMasterPageElement : OdfElement
    {
        public StyleMasterPageElement(string? prefix = null) : base("master-page", OdfNamespaces.Style, prefix) { }

        public string? Name
        {
            get => GetAttribute("name", OdfNamespaces.Style);
            set => SetAttribute("name", OdfNamespaces.Style, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        }
    }

    public partial class StylePageLayoutElement : OdfElement
    {
        public StylePageLayoutElement(string? prefix = null) : base("page-layout", OdfNamespaces.Style, prefix) { }

        public string? Name
        {
            get => GetAttribute("name", OdfNamespaces.Style);
            set => SetAttribute("name", OdfNamespaces.Style, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        }
    }

    public partial class StyleTextPropertiesElement : OdfElement
    {
        public StyleTextPropertiesElement(string? prefix = null) : base("text-properties", OdfNamespaces.Style, prefix) { }
    }

    public partial class StyleParagraphPropertiesElement : OdfElement
    {
        public StyleParagraphPropertiesElement(string? prefix = null) : base("paragraph-properties", OdfNamespaces.Style, prefix) { }
    }

    #endregion

    #region Office Wrappers

    public partial class OfficeDocumentElement : OdfElement
    {
        public OfficeDocumentElement(string? prefix = null) : base("document", OdfNamespaces.Office, prefix) { }
    }

    public partial class OfficeDocumentContentElement : OdfElement
    {
        public OfficeDocumentContentElement(string? prefix = null) : base("document-content", OdfNamespaces.Office, prefix) { }
    }

    public partial class OfficeBodyElement : OdfElement
    {
        public OfficeBodyElement(string? prefix = null) : base("body", OdfNamespaces.Office, prefix) { }
    }

    public partial class OfficeTextElement : OdfElement
    {
        public OfficeTextElement(string? prefix = null) : base("text", OdfNamespaces.Office, prefix) { }
    }

    public partial class OfficeSpreadsheetElement : OdfElement
    {
        public OfficeSpreadsheetElement(string? prefix = null) : base("spreadsheet", OdfNamespaces.Office, prefix) { }
    }

    public partial class OfficePresentationElement : OdfElement
    {
        public OfficePresentationElement(string? prefix = null) : base("presentation", OdfNamespaces.Office, prefix) { }
    }

    public partial class OfficeDrawingElement : OdfElement
    {
        public OfficeDrawingElement(string? prefix = null) : base("drawing", OdfNamespaces.Office, prefix) { }
    }

    #endregion

    #region Manifest Wrappers

    public partial class ManifestManifestElement : OdfElement
    {
        public ManifestManifestElement(string? prefix = null) : base("manifest", OdfNamespaces.Manifest, prefix) { }
    }

    public partial class ManifestFileEntryElement : OdfElement
    {
        public ManifestFileEntryElement(string? prefix = null) : base("file-entry", OdfNamespaces.Manifest, prefix) { }

        public string? FullPath
        {
            get => GetAttribute("full-path", OdfNamespaces.Manifest);
            set => SetAttribute("full-path", OdfNamespaces.Manifest, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Manifest));
        }

        public string? MediaType
        {
            get => GetAttribute("media-type", OdfNamespaces.Manifest);
            set => SetAttribute("media-type", OdfNamespaces.Manifest, value ?? string.Empty, OdfNamespaces.GetPrefix(OdfNamespaces.Manifest));
        }
    }

    public partial class ManifestEncryptionDataElement : OdfElement
    {
        public ManifestEncryptionDataElement(string? prefix = null) : base("encryption-data", OdfNamespaces.Manifest, prefix) { }
    }

    #endregion
}
