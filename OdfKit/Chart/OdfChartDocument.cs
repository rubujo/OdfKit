#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System;
using System.IO;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Chart
{
    public class OdfChartDocument : OdfDocument
    {
        public OdfChartDocument(OdfPackage package) : this(package, string.Empty)
        {
        }

        public OdfChartDocument(OdfPackage package, string subPath) : base(package, subPath)
        {
            if (string.IsNullOrEmpty(package.MimeType))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.chart");
            }
        }

        protected override string GetDefaultContentXml()
        {
            return "<office:document-content " +
                   "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
                   "xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" " +
                   "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
                   "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
                   "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" " +
                   "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
                   "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" " +
                   "xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
                   "office:version=\"1.3\">" +
                   "<office:body>" +
                   "<office:chart>" +
                   "<chart:chart chart:class=\"line\" />" +
                   "</office:chart>" +
                   "</office:body>" +
                   "</office:document-content>";
        }

        protected override string GetDefaultStylesXml()
        {
            return "<office:document-styles " +
                   "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
                   "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
                   "office:version=\"1.3\">" +
                   "<office:styles></office:styles>" +
                   "</office:document-styles>";
        }

        protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, System.Collections.Generic.Dictionary<string, string> renameMap)
        {
            var srcChart = sourceDoc as OdfChartDocument ?? throw new ArgumentException("Source document must be a OdfChartDocument.");
            
            var body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
            var destChartRoot = FindOrCreateChild(body, "chart", OdfNamespaces.Office, "office");
            
            var srcBody = srcChart.FindOrCreateChild(srcChart.ContentDom, "body", OdfNamespaces.Office, "office");
            var srcChartRoot = srcChart.FindOrCreateChild(srcBody, "chart", OdfNamespaces.Office, "office");
            
            foreach (var child in srcChartRoot.Children)
            {
                if (child.NodeType == OdfNodeType.Element)
                {
                    var imported = OdfNode.ImportNode(child, srcChart.Package, Package);
                    RemapStylesInNodes(imported, renameMap);
                    destChartRoot.AppendChild(imported);
                }
            }
        }
    }
}
