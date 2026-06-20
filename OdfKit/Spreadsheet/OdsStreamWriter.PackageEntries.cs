using System;
using System.IO;
using System.IO.Compression;
using System.Xml;
using OdfKit.Core;

namespace OdfKit.Spreadsheet;

public partial class OdsStreamWriter
{
    #region Package Entries

    private void WriteDefaultMetaFiles()
    {
        WriteManifest();
        // 此處不寫入 styles.xml
        WriteMeta();
    }

    private void WriteManifest()
    {
        var entry = _zip.CreateEntry("META-INF/manifest.xml", CompressionLevel.Optimal);
        using (var stream = entry.Open())
        using (var writer = XmlWriter.Create(stream))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("manifest", "manifest", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "version", OdfNamespaces.Manifest, FormatVersion(_version));

            writer.WriteStartElement("file-entry", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, "/");
            writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, "application/vnd.oasis.opendocument.spreadsheet");
            writer.WriteEndElement();

            writer.WriteStartElement("file-entry", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, "content.xml");
            writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, "text/xml");
            writer.WriteEndElement();

            writer.WriteStartElement("file-entry", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, "meta.xml");
            writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, "text/xml");
            writer.WriteEndElement();

            writer.WriteStartElement("file-entry", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, "styles.xml");
            writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, "text/xml");
            writer.WriteEndElement();

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }
    }

    private void WriteStyles()
    {
        var entry = _zip.CreateEntry("styles.xml", CompressionLevel.Optimal);
        using (var stream = entry.Open())
        using (var writer = XmlWriter.Create(stream))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("office", "document-styles", OdfNamespaces.Office);
            writer.WriteAttributeString("office", "version", OdfNamespaces.Office, FormatVersion(_version));
            writer.WriteAttributeString("xmlns", "office", null, OdfNamespaces.Office);
            writer.WriteAttributeString("xmlns", "style", null, OdfNamespaces.Style);
            writer.WriteAttributeString("xmlns", "text", null, OdfNamespaces.Text);
            writer.WriteAttributeString("xmlns", "table", null, OdfNamespaces.Table);
            writer.WriteAttributeString("xmlns", "fo", null, OdfNamespaces.Fo);

            writer.WriteStartElement("office", "styles", OdfNamespaces.Office);
            writer.WriteEndElement();

            writer.WriteStartElement("office", "automatic-styles", OdfNamespaces.Office);
            foreach (var style in _columnStyles)
            {
                writer.WriteStartElement("style", "style", OdfNamespaces.Style);
                writer.WriteAttributeString("style", "name", OdfNamespaces.Style, style.styleName);
                writer.WriteAttributeString("style", "family", OdfNamespaces.Style, "table-column");
                writer.WriteStartElement("style", "table-column-properties", OdfNamespaces.Style);
                writer.WriteAttributeString("style", "column-width", OdfNamespaces.Style, style.width.ToString());
                writer.WriteEndElement(); // table-column-properties
                writer.WriteEndElement(); // style
            }

            foreach (var style in _rowStyles)
            {
                writer.WriteStartElement("style", "style", OdfNamespaces.Style);
                writer.WriteAttributeString("style", "name", OdfNamespaces.Style, style.styleName);
                writer.WriteAttributeString("style", "family", OdfNamespaces.Style, "table-row");
                writer.WriteStartElement("style", "table-row-properties", OdfNamespaces.Style);
                if (style.useOptimalHeight)
                {
                    writer.WriteAttributeString("style", "use-optimal-row-height", OdfNamespaces.Style, "true");
                }
                else if (style.height.HasValue)
                {
                    string heightCm = style.height.Value.ToCentimeters()
                        .ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "cm";
                    writer.WriteAttributeString("style", "row-height", OdfNamespaces.Style, heightCm);
                }
                writer.WriteEndElement(); // table-row-properties
                writer.WriteEndElement(); // style
            }
            writer.WriteEndElement();

            writer.WriteStartElement("office", "master-styles", OdfNamespaces.Office);
            writer.WriteEndElement();

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }
    }

    private void WriteMeta()
    {
        var entry = _zip.CreateEntry("meta.xml", CompressionLevel.Optimal);
        using (var stream = entry.Open())
        using (var writer = XmlWriter.Create(stream))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("office", "document-meta", OdfNamespaces.Office);
            writer.WriteAttributeString("office", "version", OdfNamespaces.Office, FormatVersion(_version));
            writer.WriteAttributeString("xmlns", "office", null, OdfNamespaces.Office);
            writer.WriteAttributeString("xmlns", "dc", null, OdfNamespaces.Dc);
            writer.WriteAttributeString("xmlns", "meta", null, OdfNamespaces.Meta);

            writer.WriteStartElement("office", "meta", OdfNamespaces.Office);
            writer.WriteEndElement();

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }
    }

    #endregion
}
