#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
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
            get => GetAttributeValue("style-name", OdfNamespaces.Text, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("style-name", OdfNamespaces.Text);
                else
                    SetAttributeValue("style-name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
            }
        }
    }

    public partial class TextHElement : OdfElement
    {
        public TextHElement(string? prefix = null) : base("h", OdfNamespaces.Text, prefix) { }

        public string? StyleName
        {
            get => GetAttributeValue("style-name", OdfNamespaces.Text, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("style-name", OdfNamespaces.Text);
                else
                    SetAttributeValue("style-name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
            }
        }

        public int OutlineLevel
        {
            get => int.TryParse(GetAttributeValue("outline-level", OdfNamespaces.Text, GetDocumentVersion()), out var level) ? level : 1;
            set => SetAttributeValue("outline-level", OdfNamespaces.Text, value.ToString(), OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
        }
    }

    public partial class TextSpanElement : OdfElement
    {
        public TextSpanElement(string? prefix = null) : base("span", OdfNamespaces.Text, prefix) { }

        public string? StyleName
        {
            get => GetAttributeValue("style-name", OdfNamespaces.Text, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("style-name", OdfNamespaces.Text);
                else
                    SetAttributeValue("style-name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
            }
        }
    }

    public partial class TextListElement : OdfElement
    {
        public TextListElement(string? prefix = null) : base("list", OdfNamespaces.Text, prefix) { }

        public string? StyleName
        {
            get => GetAttributeValue("style-name", OdfNamespaces.Text, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("style-name", OdfNamespaces.Text);
                else
                    SetAttributeValue("style-name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
            }
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
            get => GetAttributeValue("name", OdfNamespaces.Text, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("name", OdfNamespaces.Text);
                else
                    SetAttributeValue("name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
            }
        }

        public string? StyleName
        {
            get => GetAttributeValue("style-name", OdfNamespaces.Text, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("style-name", OdfNamespaces.Text);
                else
                    SetAttributeValue("style-name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
            }
        }
    }

    public partial class TextBookmarkElement : OdfElement
    {
        public TextBookmarkElement(string? prefix = null) : base("bookmark", OdfNamespaces.Text, prefix) { }

        public string? Name
        {
            get => GetAttributeValue("name", OdfNamespaces.Text, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("name", OdfNamespaces.Text);
                else
                    SetAttributeValue("name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
            }
        }
    }

    public partial class TextNoteElement : OdfElement
    {
        public TextNoteElement(string? prefix = null) : base("note", OdfNamespaces.Text, prefix) { }

        public string? NoteClass
        {
            get => GetAttributeValue("note-class", OdfNamespaces.Text, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("note-class", OdfNamespaces.Text);
                else
                    SetAttributeValue("note-class", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
            }
        }
    }

    public partial class OfficeAnnotationElement : OdfElement
    {
        public OfficeAnnotationElement(string? prefix = null) : base("annotation", OdfNamespaces.Office, prefix) { }

        public string? Name
        {
            get => GetAttributeValue("name", OdfNamespaces.Office, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("name", OdfNamespaces.Office);
                else
                    SetAttributeValue("name", OdfNamespaces.Office, value, OdfNamespaces.GetPrefix(OdfNamespaces.Office), GetDocumentVersion());
            }
        }
    }

    #endregion

    #region Table Wrappers

    public partial class TableTableElement : OdfElement
    {
        public TableTableElement(string? prefix = null) : base("table", OdfNamespaces.Table, prefix) { }

        public string? Name
        {
            get => GetAttributeValue("name", OdfNamespaces.Table, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("name", OdfNamespaces.Table);
                else
                    SetAttributeValue("name", OdfNamespaces.Table, value, OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
            }
        }

        public string? StyleName
        {
            get => GetAttributeValue("style-name", OdfNamespaces.Table, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("style-name", OdfNamespaces.Table);
                else
                    SetAttributeValue("style-name", OdfNamespaces.Table, value, OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
            }
        }
    }

    public partial class TableTableRowElement : OdfElement
    {
        public TableTableRowElement(string? prefix = null) : base("table-row", OdfNamespaces.Table, prefix) { }

        public int NumberRowsRepeated
        {
            get => int.TryParse(GetAttributeValue("number-rows-repeated", OdfNamespaces.Table, GetDocumentVersion()), out var val) ? val : 1;
            set => SetAttributeValue("number-rows-repeated", OdfNamespaces.Table, value.ToString(), OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
        }
    }

    public partial class TableTableCellElement : OdfElement
    {
        public TableTableCellElement(string? prefix = null) : base("table-cell", OdfNamespaces.Table, prefix) { }

        public int NumberColumnsRepeated
        {
            get => int.TryParse(GetAttributeValue("number-columns-repeated", OdfNamespaces.Table, GetDocumentVersion()), out var val) ? val : 1;
            set => SetAttributeValue("number-columns-repeated", OdfNamespaces.Table, value.ToString(), OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
        }

        public string? ValueType
        {
            get => GetAttributeValue("value-type", OdfNamespaces.Office, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("value-type", OdfNamespaces.Office);
                else
                    SetAttributeValue("value-type", OdfNamespaces.Office, value, OdfNamespaces.GetPrefix(OdfNamespaces.Office), GetDocumentVersion());
            }
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
            get => GetAttributeValue("name", OdfNamespaces.Table, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("name", OdfNamespaces.Table);
                else
                    SetAttributeValue("name", OdfNamespaces.Table, value, OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
            }
        }
    }

    public partial class TableDatabaseRangeElement : OdfElement
    {
        public TableDatabaseRangeElement(string? prefix = null) : base("database-range", OdfNamespaces.Table, prefix) { }

        public string? Name
        {
            get => GetAttributeValue("name", OdfNamespaces.Table, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("name", OdfNamespaces.Table);
                else
                    SetAttributeValue("name", OdfNamespaces.Table, value, OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
            }
        }
    }

    #endregion

    #region Draw Wrappers

    public partial class DrawFrameElement : OdfElement
    {
        public DrawFrameElement(string? prefix = null) : base("frame", OdfNamespaces.Draw, prefix) { }

        public string? Name
        {
            get => GetAttributeValue("name", OdfNamespaces.Draw, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("name", OdfNamespaces.Draw);
                else
                    SetAttributeValue("name", OdfNamespaces.Draw, value, OdfNamespaces.GetPrefix(OdfNamespaces.Draw), GetDocumentVersion());
            }
        }
    }

    public partial class DrawImageElement : OdfElement
    {
        public DrawImageElement(string? prefix = null) : base("image", OdfNamespaces.Draw, prefix) { }

        public string? Href
        {
            get => GetAttributeValue("href", OdfNamespaces.XLink, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("href", OdfNamespaces.XLink);
                else
                    SetAttributeValue("href", OdfNamespaces.XLink, value, OdfNamespaces.GetPrefix(OdfNamespaces.XLink), GetDocumentVersion());
            }
        }
    }

    public partial class DrawObjectElement : OdfElement
    {
        public DrawObjectElement(string? prefix = null) : base("object", OdfNamespaces.Draw, prefix) { }

        public string? Href
        {
            get => GetAttributeValue("href", OdfNamespaces.XLink, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("href", OdfNamespaces.XLink);
                else
                    SetAttributeValue("href", OdfNamespaces.XLink, value, OdfNamespaces.GetPrefix(OdfNamespaces.XLink), GetDocumentVersion());
            }
        }
    }

    public partial class DrawShapeElement : OdfElement
    {
        public DrawShapeElement(string shapeKind, string? prefix = null) : base(shapeKind, OdfNamespaces.Draw, prefix) { }

        public string? Name
        {
            get => GetAttributeValue("name", OdfNamespaces.Draw, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("name", OdfNamespaces.Draw);
                else
                    SetAttributeValue("name", OdfNamespaces.Draw, value, OdfNamespaces.GetPrefix(OdfNamespaces.Draw), GetDocumentVersion());
            }
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
            get => GetAttributeValue("name", OdfNamespaces.Style, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("name", OdfNamespaces.Style);
                else
                    SetAttributeValue("name", OdfNamespaces.Style, value, OdfNamespaces.GetPrefix(OdfNamespaces.Style), GetDocumentVersion());
            }
        }

        public string? Family
        {
            get => GetAttributeValue("family", OdfNamespaces.Style, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("family", OdfNamespaces.Style);
                else
                    SetAttributeValue("family", OdfNamespaces.Style, value, OdfNamespaces.GetPrefix(OdfNamespaces.Style), GetDocumentVersion());
            }
        }
    }

    public partial class StyleDefaultStyleElement : OdfElement
    {
        public StyleDefaultStyleElement(string? prefix = null) : base("default-style", OdfNamespaces.Style, prefix) { }

        public string? Family
        {
            get => GetAttributeValue("family", OdfNamespaces.Style, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("family", OdfNamespaces.Style);
                else
                    SetAttributeValue("family", OdfNamespaces.Style, value, OdfNamespaces.GetPrefix(OdfNamespaces.Style), GetDocumentVersion());
            }
        }
    }

    public partial class StyleMasterPageElement : OdfElement
    {
        public StyleMasterPageElement(string? prefix = null) : base("master-page", OdfNamespaces.Style, prefix) { }

        public string? Name
        {
            get => GetAttributeValue("name", OdfNamespaces.Style, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("name", OdfNamespaces.Style);
                else
                    SetAttributeValue("name", OdfNamespaces.Style, value, OdfNamespaces.GetPrefix(OdfNamespaces.Style), GetDocumentVersion());
            }
        }
    }

    public partial class StylePageLayoutElement : OdfElement
    {
        public StylePageLayoutElement(string? prefix = null) : base("page-layout", OdfNamespaces.Style, prefix) { }

        public string? Name
        {
            get => GetAttributeValue("name", OdfNamespaces.Style, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("name", OdfNamespaces.Style);
                else
                    SetAttributeValue("name", OdfNamespaces.Style, value, OdfNamespaces.GetPrefix(OdfNamespaces.Style), GetDocumentVersion());
            }
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
            get => GetAttributeValue("full-path", OdfNamespaces.Manifest, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("full-path", OdfNamespaces.Manifest);
                else
                    SetAttributeValue("full-path", OdfNamespaces.Manifest, value, OdfNamespaces.GetPrefix(OdfNamespaces.Manifest), GetDocumentVersion());
            }
        }

        public string? MediaType
        {
            get => GetAttributeValue("media-type", OdfNamespaces.Manifest, GetDocumentVersion());
            set
            {
                if (value == null)
                    RemoveAttribute("media-type", OdfNamespaces.Manifest);
                else
                    SetAttributeValue("media-type", OdfNamespaces.Manifest, value, OdfNamespaces.GetPrefix(OdfNamespaces.Manifest), GetDocumentVersion());
            }
        }
    }

    public partial class ManifestEncryptionDataElement : OdfElement
    {
        public ManifestEncryptionDataElement(string? prefix = null) : base("encryption-data", OdfNamespaces.Manifest, prefix) { }
    }

    #endregion
}
