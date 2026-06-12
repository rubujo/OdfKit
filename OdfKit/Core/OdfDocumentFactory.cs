#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System;
using System.IO;
using System.Text;
using System.Xml;
using OdfKit.Compliance;

namespace OdfKit.Core
{
    /// <summary>
    /// Creates minimal low-level ODF package and flat XML documents.
    /// </summary>
    public static class OdfDocumentFactory
    {
        /// <summary>
        /// Creates a minimal packaged ODF document in the supplied stream.
        /// </summary>
        public static OdfPackage CreatePackage(
            Stream stream,
            OdfDocumentKind kind,
            OdfVersion version = OdfVersion.Odf14,
            bool leaveOpen = false,
            OdfSaveOptions? options = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (OdfDocumentKindDetector.IsFlatKind(kind)) throw new ArgumentException("Flat ODF kinds must be created with WriteFlatXml.", nameof(kind));

            var package = OdfPackage.Create(stream, leaveOpen, options);
            InitializeMinimalPackage(package, kind, version);
            return package;
        }

        /// <summary>
        /// Creates a minimal packaged ODF document at the supplied path.
        /// </summary>
        public static OdfPackage CreatePackage(
            string path,
            OdfDocumentKind kind,
            OdfVersion version = OdfVersion.Odf14,
            OdfSaveOptions? options = null)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (OdfDocumentKindDetector.IsFlatKind(kind)) throw new ArgumentException("Flat ODF kinds must be created with WriteFlatXml.", nameof(kind));

            var package = OdfPackage.Create(path, options);
            InitializeMinimalPackage(package, kind, version);
            return package;
        }

        /// <summary>
        /// Writes a minimal flat XML ODF document to the supplied stream.
        /// </summary>
        public static void WriteFlatXml(
            Stream stream,
            OdfDocumentKind kind,
            OdfVersion version = OdfVersion.Odf14,
            bool leaveOpen = true)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            OdfDocumentKind flatKind = OdfDocumentKindDetector.ToFlatKind(kind);
            if (!OdfDocumentKindDetector.IsFlatKind(flatKind))
            {
                throw new ArgumentException("The supplied document kind does not have a flat XML ODF form.", nameof(kind));
            }

            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = false,
                CloseOutput = !leaveOpen
            };

            using XmlWriter writer = XmlWriter.Create(stream, settings);
            string mimeType = GetMimeType(GetPackagedKind(flatKind));
            string versionText = FormatVersion(version);
            string bodyElement = GetBodyElementName(flatKind);

            writer.WriteStartDocument();
            writer.WriteStartElement("office", "document", OdfNamespaces.Office);
            WriteCommonNamespaces(writer);
            writer.WriteAttributeString("office", "mimetype", OdfNamespaces.Office, mimeType);
            writer.WriteAttributeString("office", "version", OdfNamespaces.Office, versionText);
            writer.WriteStartElement("office", "body", OdfNamespaces.Office);
            writer.WriteStartElement("office", bodyElement, OdfNamespaces.Office);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        /// <summary>
        /// Populates a package with the minimal ODF entries for the supplied document kind.
        /// </summary>
        public static void InitializeMinimalPackage(
            OdfPackage package,
            OdfDocumentKind kind,
            OdfVersion version = OdfVersion.Odf14)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            if (OdfDocumentKindDetector.IsFlatKind(kind)) throw new ArgumentException("Flat ODF kinds cannot be stored as packaged ODF kinds.", nameof(kind));

            string mimeType = GetMimeType(kind);
            string versionText = FormatVersion(version);

            package.SetMimeType(mimeType);
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(CreateContentXml(kind, versionText)), "text/xml");
            package.WriteEntry("styles.xml", Encoding.UTF8.GetBytes(CreateStylesXml(versionText)), "text/xml");
            package.WriteEntry("meta.xml", Encoding.UTF8.GetBytes(CreateMetaXml(versionText)), "text/xml");
            package.WriteEntry("settings.xml", Encoding.UTF8.GetBytes(CreateSettingsXml(versionText)), "text/xml");
        }

        private static string CreateContentXml(OdfDocumentKind kind, string version)
        {
            string bodyElement = GetBodyElementName(kind);
            return "<office:document-content" +
                CommonNamespaceAttributes +
                " office:version=\"" + version + "\"><office:body><office:" +
                bodyElement +
                " /></office:body></office:document-content>";
        }

        private static string CreateStylesXml(string version)
        {
            return "<office:document-styles" +
                CommonNamespaceAttributes +
                " office:version=\"" + version + "\"><office:styles /><office:automatic-styles /><office:master-styles /></office:document-styles>";
        }

        private static string CreateMetaXml(string version)
        {
            return "<office:document-meta" +
                CommonNamespaceAttributes +
                " office:version=\"" + version + "\"><office:meta /></office:document-meta>";
        }

        private static string CreateSettingsXml(string version)
        {
            return "<office:document-settings" +
                CommonNamespaceAttributes +
                " office:version=\"" + version + "\"><office:settings /></office:document-settings>";
        }

        private static string GetMimeType(OdfDocumentKind kind)
        {
            return kind switch
            {
                OdfDocumentKind.Text => "application/vnd.oasis.opendocument.text",
                OdfDocumentKind.TextTemplate => "application/vnd.oasis.opendocument.text-template",
                OdfDocumentKind.TextMaster => "application/vnd.oasis.opendocument.text-master",
                OdfDocumentKind.Spreadsheet => "application/vnd.oasis.opendocument.spreadsheet",
                OdfDocumentKind.SpreadsheetTemplate => "application/vnd.oasis.opendocument.spreadsheet-template",
                OdfDocumentKind.Presentation => "application/vnd.oasis.opendocument.presentation",
                OdfDocumentKind.PresentationTemplate => "application/vnd.oasis.opendocument.presentation-template",
                OdfDocumentKind.Graphics => "application/vnd.oasis.opendocument.graphics",
                OdfDocumentKind.GraphicsTemplate => "application/vnd.oasis.opendocument.graphics-template",
                OdfDocumentKind.Chart => "application/vnd.oasis.opendocument.chart",
                OdfDocumentKind.Formula => "application/vnd.oasis.opendocument.formula",
                OdfDocumentKind.Image => "application/vnd.oasis.opendocument.image",
                OdfDocumentKind.Database => "application/vnd.oasis.opendocument.database",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported ODF document kind.")
            };
        }

        private static string GetBodyElementName(OdfDocumentKind kind)
        {
            return kind switch
            {
                OdfDocumentKind.Text or OdfDocumentKind.TextTemplate or OdfDocumentKind.TextMaster or OdfDocumentKind.FlatText => "text",
                OdfDocumentKind.Spreadsheet or OdfDocumentKind.SpreadsheetTemplate or OdfDocumentKind.FlatSpreadsheet => "spreadsheet",
                OdfDocumentKind.Presentation or OdfDocumentKind.PresentationTemplate or OdfDocumentKind.FlatPresentation => "presentation",
                OdfDocumentKind.Graphics or OdfDocumentKind.GraphicsTemplate or OdfDocumentKind.FlatGraphics => "drawing",
                OdfDocumentKind.Chart => "chart",
                OdfDocumentKind.Formula => "formula",
                OdfDocumentKind.Image => "image",
                OdfDocumentKind.Database => "database",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported ODF document kind.")
            };
        }

        private static OdfDocumentKind GetPackagedKind(OdfDocumentKind kind)
        {
            return kind switch
            {
                OdfDocumentKind.FlatText => OdfDocumentKind.Text,
                OdfDocumentKind.FlatSpreadsheet => OdfDocumentKind.Spreadsheet,
                OdfDocumentKind.FlatPresentation => OdfDocumentKind.Presentation,
                OdfDocumentKind.FlatGraphics => OdfDocumentKind.Graphics,
                _ => kind
            };
        }

        private static string FormatVersion(OdfVersion version)
        {
            return version switch
            {
                OdfVersion.Odf10 => "1.0",
                OdfVersion.Odf11 => "1.1",
                OdfVersion.Odf12 => "1.2",
                OdfVersion.Odf13 => "1.3",
                OdfVersion.Odf14 => "1.4",
                _ => throw new ArgumentOutOfRangeException(nameof(version), version, "A concrete ODF version is required.")
            };
        }

        private static void WriteCommonNamespaces(XmlWriter writer)
        {
            writer.WriteAttributeString("xmlns", "style", null, OdfNamespaces.Style);
            writer.WriteAttributeString("xmlns", "text", null, OdfNamespaces.Text);
            writer.WriteAttributeString("xmlns", "table", null, OdfNamespaces.Table);
            writer.WriteAttributeString("xmlns", "draw", null, OdfNamespaces.Draw);
            writer.WriteAttributeString("xmlns", "fo", null, OdfNamespaces.Fo);
            writer.WriteAttributeString("xmlns", "xlink", null, OdfNamespaces.XLink);
            writer.WriteAttributeString("xmlns", "dc", null, OdfNamespaces.Dc);
            writer.WriteAttributeString("xmlns", "meta", null, OdfNamespaces.Meta);
            writer.WriteAttributeString("xmlns", "number", null, OdfNamespaces.Number);
            writer.WriteAttributeString("xmlns", "presentation", null, OdfNamespaces.Presentation);
            writer.WriteAttributeString("xmlns", "svg", null, OdfNamespaces.Svg);
            writer.WriteAttributeString("xmlns", "chart", null, OdfNamespaces.Chart);
            writer.WriteAttributeString("xmlns", "config", null, OdfNamespaces.Config);
        }

        private const string CommonNamespaceAttributes =
            " xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"" +
            " xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\"" +
            " xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"" +
            " xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\"" +
            " xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\"" +
            " xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\"" +
            " xmlns:xlink=\"http://www.w3.org/1999/xlink\"" +
            " xmlns:dc=\"http://purl.org/dc/elements/1.1/\"" +
            " xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\"" +
            " xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\"" +
            " xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\"" +
            " xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\"" +
            " xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\"" +
            " xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\"";
    }
}
