using System;
using System.IO;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text
{
    public class OdfFormulaObject
    {
        public OdfNode FrameNode { get; }
        public OdfNode ObjectNode { get; }
        private readonly TextDocument _doc;

        public OdfFormulaObject(OdfNode frameNode, OdfNode objectNode, TextDocument doc)
        {
            FrameNode = frameNode ?? throw new ArgumentNullException(nameof(frameNode));
            ObjectNode = objectNode ?? throw new ArgumentNullException(nameof(objectNode));
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public string? Name
        {
            get => FrameNode.GetAttribute("name", OdfNamespaces.Draw);
            set => FrameNode.SetAttribute("name", OdfNamespaces.Draw, value ?? string.Empty, "draw");
        }

        public string? AnchorType
        {
            get => FrameNode.GetAttribute("anchor-type", OdfNamespaces.Text);
            set => FrameNode.SetAttribute("anchor-type", OdfNamespaces.Text, value ?? "as-char", "text");
        }

        public string? Width
        {
            get => FrameNode.GetAttribute("width", OdfNamespaces.Svg);
            set => FrameNode.SetAttribute("width", OdfNamespaces.Svg, value ?? string.Empty, "svg");
        }

        public string? Height
        {
            get => FrameNode.GetAttribute("height", OdfNamespaces.Svg);
            set => FrameNode.SetAttribute("height", OdfNamespaces.Svg, value ?? string.Empty, "svg");
        }

        public string? FormulaFolder
        {
            get => ObjectNode.GetAttribute("href", OdfNamespaces.XLink);
            set
            {
                if (value != null)
                {
                    // Zip Slip Defense: enforce strict path validation on folder assignment
                    if (value.Contains("..") || value.Contains("\\") || value.StartsWith("/"))
                    {
                        throw new InvalidOperationException("Invalid formula folder path specified (Zip Slip defense).");
                    }
                    ObjectNode.SetAttribute("href", OdfNamespaces.XLink, value, "xlink");
                }
                else
                {
                    ObjectNode.RemoveAttribute("href", OdfNamespaces.XLink);
                }
            }
        }

        public string MathMlXmlString
        {
            get
            {
                string? folder = FormulaFolder;
                if (folder == null || folder.Length == 0) return string.Empty;
                string contentPath = $"{folder.TrimEnd('/')}/content.xml";
                if (!_doc.Package.HasEntry(contentPath)) return string.Empty;
                
                var bytes = _doc.Package.ReadEntry(contentPath);
                if (bytes == null) return string.Empty;
                
                string xml = Encoding.UTF8.GetString(bytes);
                return ExtractFormulaContent(xml);
            }
            set
            {
                string? folder = FormulaFolder;
                if (folder == null || folder.Length == 0)
                {
                    folder = $"Formula_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                    FormulaFolder = folder;
                }

                // Defensive check against Zip Slip directory traversal
                if (folder.Contains("..") || folder.Contains("\\") || folder.StartsWith("/"))
                {
                    throw new InvalidOperationException("Invalid formula folder path specified (Zip Slip defense).");
                }

                string mathDocXml = $"<office:document-meta xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:math=\"http://www.w3.org/1998/Math/MathML\"><office:body><office:formula>{value}</office:formula></office:body></office:document-meta>";

                string contentPath = $"{folder.TrimEnd('/')}/content.xml";
                string mimePath = $"{folder.TrimEnd('/')}/mimetype";

                _doc.Package.WriteEntry(contentPath, Encoding.UTF8.GetBytes(mathDocXml), "text/xml");
                _doc.Package.WriteEntry(mimePath, Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.formula"), "application/vnd.oasis.opendocument.formula");
                _doc.Package.SaveManifestToEntries();
            }
        }

        private string ExtractFormulaContent(string documentXml)
        {
            if (string.IsNullOrWhiteSpace(documentXml)) return string.Empty;
            
            int start = documentXml.IndexOf("<office:formula>");
            if (start == -1) start = documentXml.IndexOf("<office:formula ");
            if (start == -1) return string.Empty;

            int closeTagStart = documentXml.IndexOf('>', start);
            if (closeTagStart == -1) return string.Empty;

            int end = documentXml.IndexOf("</office:formula>", closeTagStart);
            if (end == -1) return string.Empty;

            return documentXml.Substring(closeTagStart + 1, end - closeTagStart - 1).Trim();
        }
    }
}
