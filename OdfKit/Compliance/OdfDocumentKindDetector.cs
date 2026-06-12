#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System;
using System.Collections.Generic;
using System.IO;

namespace OdfKit.Compliance
{
    /// <summary>
    /// Detects ODF document kinds from MIME types and file extensions.
    /// </summary>
    public static class OdfDocumentKindDetector
    {
        private static readonly Dictionary<string, OdfDocumentKind> MimeTypeKinds = new Dictionary<string, OdfDocumentKind>(StringComparer.Ordinal)
        {
            ["application/vnd.oasis.opendocument.text"] = OdfDocumentKind.Text,
            ["application/vnd.oasis.opendocument.text-template"] = OdfDocumentKind.TextTemplate,
            ["application/vnd.oasis.opendocument.text-master"] = OdfDocumentKind.TextMaster,
            ["application/vnd.oasis.opendocument.spreadsheet"] = OdfDocumentKind.Spreadsheet,
            ["application/vnd.oasis.opendocument.spreadsheet-template"] = OdfDocumentKind.SpreadsheetTemplate,
            ["application/vnd.oasis.opendocument.presentation"] = OdfDocumentKind.Presentation,
            ["application/vnd.oasis.opendocument.presentation-template"] = OdfDocumentKind.PresentationTemplate,
            ["application/vnd.oasis.opendocument.graphics"] = OdfDocumentKind.Graphics,
            ["application/vnd.oasis.opendocument.graphics-template"] = OdfDocumentKind.GraphicsTemplate,
            ["application/vnd.oasis.opendocument.chart"] = OdfDocumentKind.Chart,
            ["application/vnd.oasis.opendocument.formula"] = OdfDocumentKind.Formula,
            ["application/vnd.oasis.opendocument.image"] = OdfDocumentKind.Image,
            ["application/vnd.oasis.opendocument.database"] = OdfDocumentKind.Database
        };

        private static readonly Dictionary<string, OdfDocumentKind> ExtensionKinds = new Dictionary<string, OdfDocumentKind>(StringComparer.OrdinalIgnoreCase)
        {
            [".odt"] = OdfDocumentKind.Text,
            [".ott"] = OdfDocumentKind.TextTemplate,
            [".odm"] = OdfDocumentKind.TextMaster,
            [".ods"] = OdfDocumentKind.Spreadsheet,
            [".ots"] = OdfDocumentKind.SpreadsheetTemplate,
            [".odp"] = OdfDocumentKind.Presentation,
            [".otp"] = OdfDocumentKind.PresentationTemplate,
            [".odg"] = OdfDocumentKind.Graphics,
            [".otg"] = OdfDocumentKind.GraphicsTemplate,
            [".odc"] = OdfDocumentKind.Chart,
            [".odf"] = OdfDocumentKind.Formula,
            [".odi"] = OdfDocumentKind.Image,
            [".odb"] = OdfDocumentKind.Database,
            [".fodt"] = OdfDocumentKind.FlatText,
            [".fods"] = OdfDocumentKind.FlatSpreadsheet,
            [".fodp"] = OdfDocumentKind.FlatPresentation,
            [".fodg"] = OdfDocumentKind.FlatGraphics
        };

        /// <summary>
        /// Detects the packaged ODF document kind from an ODF MIME type.
        /// </summary>
        public static OdfDocumentKind FromMimeType(string? mimeType)
        {
            return mimeType != null && MimeTypeKinds.TryGetValue(mimeType, out OdfDocumentKind kind)
                ? kind
                : OdfDocumentKind.Unknown;
        }

        /// <summary>
        /// Detects the ODF document kind from a file name or extension.
        /// </summary>
        public static OdfDocumentKind FromFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return OdfDocumentKind.Unknown;
            }

            string extension = Path.GetExtension(fileName);
            return ExtensionKinds.TryGetValue(extension, out OdfDocumentKind kind)
                ? kind
                : OdfDocumentKind.Unknown;
        }

        /// <summary>
        /// Converts a packaged ODF kind to its flat XML kind when a flat form exists.
        /// </summary>
        public static OdfDocumentKind ToFlatKind(OdfDocumentKind kind)
        {
            return kind switch
            {
                OdfDocumentKind.Text => OdfDocumentKind.FlatText,
                OdfDocumentKind.Spreadsheet => OdfDocumentKind.FlatSpreadsheet,
                OdfDocumentKind.Presentation => OdfDocumentKind.FlatPresentation,
                OdfDocumentKind.Graphics => OdfDocumentKind.FlatGraphics,
                _ => kind
            };
        }

        /// <summary>
        /// Converts template, master, and flat variants to the content kind expressed under office:body.
        /// </summary>
        public static OdfDocumentKind ToContentKind(OdfDocumentKind kind)
        {
            return kind switch
            {
                OdfDocumentKind.TextTemplate or OdfDocumentKind.TextMaster or OdfDocumentKind.FlatText => OdfDocumentKind.Text,
                OdfDocumentKind.SpreadsheetTemplate or OdfDocumentKind.FlatSpreadsheet => OdfDocumentKind.Spreadsheet,
                OdfDocumentKind.PresentationTemplate or OdfDocumentKind.FlatPresentation => OdfDocumentKind.Presentation,
                OdfDocumentKind.GraphicsTemplate or OdfDocumentKind.FlatGraphics => OdfDocumentKind.Graphics,
                _ => kind
            };
        }

        /// <summary>
        /// Detects the content kind from the first ODF child element under office:body.
        /// </summary>
        public static OdfDocumentKind FromOfficeBodyElement(string? localName, bool flat)
        {
            OdfDocumentKind packagedKind = localName switch
            {
                "text" => OdfDocumentKind.Text,
                "spreadsheet" => OdfDocumentKind.Spreadsheet,
                "presentation" => OdfDocumentKind.Presentation,
                "drawing" => OdfDocumentKind.Graphics,
                "chart" => OdfDocumentKind.Chart,
                "formula" => OdfDocumentKind.Formula,
                "image" => OdfDocumentKind.Image,
                "database" => OdfDocumentKind.Database,
                _ => OdfDocumentKind.Unknown
            };

            return flat ? ToFlatKind(packagedKind) : packagedKind;
        }

        /// <summary>
        /// Returns true when a declared document kind matches a detected office:body kind.
        /// </summary>
        public static bool IsCompatibleWithBodyKind(OdfDocumentKind declaredKind, OdfDocumentKind bodyKind)
        {
            if (declaredKind == OdfDocumentKind.Unknown || bodyKind == OdfDocumentKind.Unknown)
            {
                return true;
            }

            return ToContentKind(declaredKind) == ToContentKind(bodyKind);
        }

        /// <summary>
        /// Returns true when the supplied kind represents a flat XML ODF document.
        /// </summary>
        public static bool IsFlatKind(OdfDocumentKind kind)
        {
            return kind == OdfDocumentKind.FlatText ||
                kind == OdfDocumentKind.FlatSpreadsheet ||
                kind == OdfDocumentKind.FlatPresentation ||
                kind == OdfDocumentKind.FlatGraphics;
        }
    }
}
