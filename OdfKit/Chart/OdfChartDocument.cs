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
            throw new NotImplementedException();
        }
    }
}
