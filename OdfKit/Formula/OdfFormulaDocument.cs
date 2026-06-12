#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System;
using System.IO;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Formula
{
    public class OdfFormulaDocument : OdfDocument
    {
        public OdfFormulaDocument(OdfPackage package) : this(package, string.Empty)
        {
        }

        public OdfFormulaDocument(OdfPackage package, string subPath) : base(package, subPath)
        {
            if (string.IsNullOrEmpty(package.MimeType))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.formula");
            }
        }

        protected override string GetDefaultContentXml()
        {
            return "<office:document-content " +
                   "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
                   "xmlns:math=\"http://www.w3.org/1998/Math/MathML\" " +
                   "office:version=\"1.3\">" +
                   "<office:body>" +
                   "<office:formula>" +
                   "<math:math />" +
                   "</office:formula>" +
                   "</office:body>" +
                   "</office:document-content>";
        }

        protected override string GetDefaultStylesXml()
        {
            return "<office:document-styles " +
                   "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
                   "office:version=\"1.3\">" +
                   "<office:styles></office:styles>" +
                   "</office:document-styles>";
        }

        protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, System.Collections.Generic.Dictionary<string, string> renameMap)
        {
            var srcFormula = sourceDoc as OdfFormulaDocument ?? throw new ArgumentException("Source document must be a OdfFormulaDocument.");
            
            var body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
            var destFormulaRoot = FindOrCreateChild(body, "formula", OdfNamespaces.Office, "office");
            
            var srcBody = srcFormula.FindOrCreateChild(srcFormula.ContentDom, "body", OdfNamespaces.Office, "office");
            var srcFormulaRoot = srcFormula.FindOrCreateChild(srcBody, "formula", OdfNamespaces.Office, "office");
            
            foreach (var child in srcFormulaRoot.Children)
            {
                if (child.NodeType == OdfNodeType.Element)
                {
                    var imported = OdfNode.ImportNode(child, srcFormula.Package, Package);
                    RemapStylesInNodes(imported, renameMap);
                    destFormulaRoot.AppendChild(imported);
                }
            }
        }
    }
}
