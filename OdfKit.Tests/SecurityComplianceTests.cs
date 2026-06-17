using System;
using System.IO;
using System.Security;
using System.Text;
using Xunit;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;

namespace OdfKit.Tests
{
    public class SecurityComplianceTests
    {
        [Theory]
        [InlineData("foo:bar")]
        [InlineData("foo//bar")]
        [InlineData("foo\\\\bar")]
        [InlineData("../foo")]
        [InlineData("foo/../bar")]
        [InlineData("foo/..")]
        [InlineData("..\\foo")]
        [InlineData("foo\\..\\bar")]
        [InlineData("foo\\..")]
        [InlineData("..")]
        public void TestZipSlipDefense_RejectsInvalidPaths(string invalidPath)
        {
            Assert.Throws<SecurityException>(() => OdfPackage.SanitizeEntryName(invalidPath));
        }

        [Theory]
        [InlineData("content.xml")]
        [InlineData("Pictures/image.png")]
        [InlineData("Object_1/content.xml")]
        public void TestZipSlipDefense_AcceptsValidPaths(string validPath)
        {
            var result = OdfPackage.SanitizeEntryName(validPath);
            Assert.Equal(validPath.Replace('\\', '/'), result);
        }

        [Fact]
        public void TestXmlReaderRejectsLargeTextDoSCorpus()
        {
            string xml =
                "<office:document-content xmlns:office=\"" + OdfNamespaces.Office + "\">" +
                "  <office:body><office:text>" + new string('x', 512) + "</office:text></office:body>" +
                "</office:document-content>";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            OdfLoadOptions options = new()
            {
                MaxXmlCharactersInDocument = 256
            };

            SecurityException exception = Assert.Throws<SecurityException>(() => OdfXmlReader.Parse(stream, options));
            Assert.Contains("character limit exceeded", exception.Message);
        }

        [Fact]
        public void TestDocumentLoadAppliesXmlCharacterLimit()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                string contentXml =
                    "<office:document-content xmlns:office=\"" + OdfNamespaces.Office + "\">" +
                    "  <office:body><office:text>" + new string('x', 1024) + "</office:text></office:body>" +
                    "</office:document-content>";
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(contentXml), "text/xml");
                package.Save();
            }

            ms.Position = 0;
            OdfLoadOptions options = new()
            {
                MaxXmlCharactersInDocument = 512
            };

            SecurityException exception = Assert.Throws<SecurityException>(
                () => OdfDocumentFactory.LoadDocument(ms, options, "large-text.odt"));
            Assert.Contains("character limit exceeded", exception.Message);
        }

        [Fact]
        public void TestMacroSanitization_CleansPackageMacrosAndSignatures()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("basic/Standard/script.xlb", Encoding.UTF8.GetBytes("<script/>"), "text/xml");
                package.WriteEntry("META-INF/macrosignatures.xml", Encoding.UTF8.GetBytes("<sig/>"), "text/xml");
                package.WriteEntry("META-INF/documentsignatures.xml", Encoding.UTF8.GetBytes("<sig/>"), "text/xml");

                string contentXml =
                    "<office:document-content xmlns:office=\"" + OdfNamespaces.Office + "\" " +
                    "xmlns:xlink=\"" + OdfNamespaces.XLink + "\" xmlns:script=\"urn:oasis:names:tc:opendocument:xmlns:script:1.0\" " +
                    "xmlns:draw=\"" + OdfNamespaces.Draw + "\">" +
                    "  <office:body>" +
                    "    <office:event-listeners>" +
                    "      <script:event-listener script:event-name=\"on-load\" xlink:href=\"vnd.sun.star.script:Standard.Module1.Main\"/>" +
                    "    </office:event-listeners>" +
                    "    <script:script-data script:language=\"ooo:StarBasic\" />" +
                    "    <draw:rect xlink:href=\"vnd.sun.star.script:Standard.Module1.Click\" xlink:type=\"simple\" script:custom-macro=\"ooo:StarBasic\"/>" +
                    "    <draw:circle xlink:href=\"Pictures/image.png\" xlink:type=\"simple\"/>" +
                    "  </office:body>" +
                    "</office:document-content>";

                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(contentXml), "text/xml");
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                Assert.True(package.HasEntry("basic/Standard/script.xlb"));
                Assert.True(package.HasEntry("META-INF/macrosignatures.xml"));

                // Perform sanitization
                package.SanitizeMacros();

                Assert.False(package.HasEntry("basic/Standard/script.xlb"));
                Assert.False(package.HasEntry("META-INF/macrosignatures.xml"));
                Assert.False(package.HasEntry("META-INF/documentsignatures.xml"));

                // Verify XML contents
                using var stream = package.GetEntryStream("content.xml");
                var docNode = OdfXmlReader.Parse(stream);

                // 1. Verify event-listeners are gone
                var eventListeners = FindNode(docNode, "event-listeners");
                Assert.Null(eventListeners);

                // 2. Verify script:script-data is gone (since it is in script namespace)
                var scriptDataNode = FindNode(docNode, "script-data");
                Assert.Null(scriptDataNode);

                // 3. Verify rect xlink:href and script:custom-macro attributes are stripped
                var rectNode = FindNode(docNode, "rect");
                Assert.NotNull(rectNode);
                Assert.Null(rectNode.GetAttribute("href", OdfNamespaces.XLink));
                Assert.Null(rectNode.GetAttribute("custom-macro", "urn:oasis:names:tc:opendocument:xmlns:script:1.0"));

                // 4. Verify normal link (image.png) is kept
                var circleNode = FindNode(docNode, "circle");
                Assert.NotNull(circleNode);
                Assert.Equal("Pictures/image.png", circleNode.GetAttribute("href", OdfNamespaces.XLink));
            }
        }

        [Fact]
        public void TestMacroSanitization_DocumentLevel()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("basic/Standard/script.xlb", Encoding.UTF8.GetBytes("<script/>"), "text/xml");
                package.WriteEntry("META-INF/macrosignatures.xml", Encoding.UTF8.GetBytes("<sig/>"), "text/xml");
                package.WriteEntry("META-INF/documentsignatures.xml", Encoding.UTF8.GetBytes("<sig/>"), "text/xml");

                string contentXml =
                    "<office:document-content xmlns:office=\"" + OdfNamespaces.Office + "\" " +
                    "xmlns:xlink=\"" + OdfNamespaces.XLink + "\" xmlns:script=\"urn:oasis:names:tc:opendocument:xmlns:script:1.0\" " +
                    "xmlns:draw=\"" + OdfNamespaces.Draw + "\">" +
                    "  <office:body>" +
                    "    <office:event-listeners>" +
                    "      <script:event-listener script:event-name=\"on-load\" xlink:href=\"vnd.sun.star.script:Standard.Module1.Main\"/>" +
                    "    </office:event-listeners>" +
                    "    <draw:rect xlink:href=\"vnd.sun.star.script:Standard.Module1.Click\" xlink:type=\"simple\"/>" +
                    "    <draw:circle xlink:href=\"Pictures/image.png\" xlink:type=\"simple\"/>" +
                    "  </office:body>" +
                    "</office:document-content>";

                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(contentXml), "text/xml");
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                using (var doc = new TextDocument(package))
                {
                    // Verify starting elements exist in ContentDom
                    var eventListenersBefore = FindNode(doc.ContentDom, "event-listeners");
                    Assert.NotNull(eventListenersBefore);

                    // Sanitize
                    doc.SanitizeMacros();

                    // Verify ContentDom is updated in memory
                    var eventListenersAfter = FindNode(doc.ContentDom, "event-listeners");
                    Assert.Null(eventListenersAfter);

                    var rectNode = FindNode(doc.ContentDom, "rect");
                    Assert.NotNull(rectNode);
                    Assert.Null(rectNode.GetAttribute("href", OdfNamespaces.XLink));

                    var circleNode = FindNode(doc.ContentDom, "circle");
                    Assert.NotNull(circleNode);
                    Assert.Equal("Pictures/image.png", circleNode.GetAttribute("href", OdfNamespaces.XLink));

                    // Verify package level macro files are removed
                    Assert.False(doc.Package.HasEntry("basic/Standard/script.xlb"));
                    Assert.False(doc.Package.HasEntry("META-INF/macrosignatures.xml"));
                    Assert.False(doc.Package.HasEntry("META-INF/documentsignatures.xml"));

                    // Save document and verify persisted package entries
                    doc.Save();
                }
            }

            // Reopen package from memory stream and verify saved entries are clean
            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                Assert.False(package.HasEntry("basic/Standard/script.xlb"));
                Assert.False(package.HasEntry("META-INF/macrosignatures.xml"));

                using var stream = package.GetEntryStream("content.xml");
                var docNode = OdfXmlReader.Parse(stream);

                var eventListeners = FindNode(docNode, "event-listeners");
                Assert.Null(eventListeners);

                var rectNode = FindNode(docNode, "rect");
                Assert.NotNull(rectNode);
                Assert.Null(rectNode.GetAttribute("href", OdfNamespaces.XLink));
            }
        }

        [Fact]
        public void TestScriptsSanitization_CleansScriptsFolder()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("Scripts/python/macro.py", Encoding.UTF8.GetBytes("print('hello')"), "text/plain");
                package.WriteEntry("basic/Standard/script.xlb", Encoding.UTF8.GetBytes("<script/>"), "text/xml");
                package.WriteEntry("META-INF/macrosignatures.xml", Encoding.UTF8.GetBytes("<sig/>"), "text/xml");
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                Assert.True(package.HasEntry("Scripts/python/macro.py"));
                Assert.True(package.HasEntry("basic/Standard/script.xlb"));

                package.SanitizeMacros();

                Assert.False(package.HasEntry("Scripts/python/macro.py"));
                Assert.False(package.HasEntry("basic/Standard/script.xlb"));
                Assert.False(package.HasEntry("META-INF/macrosignatures.xml"));
            }
        }

        [Fact]
        public void TestXLinkHrefBackslashSanitization()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");

                string contentXml =
                    "<office:document-content xmlns:office=\"" + OdfNamespaces.Office + "\" " +
                    "xmlns:xlink=\"" + OdfNamespaces.XLink + "\" xmlns:draw=\"" + OdfNamespaces.Draw + "\">" +
                    "  <office:body>" +
                    "    <draw:rect xlink:href=\"basic\\\\Standard\\\\script.xlb\" />" +
                    "    <draw:circle xlink:href=\"Scripts\\\\python\\\\macro.py\" />" +
                    "  </office:body>" +
                    "</office:document-content>";

                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(contentXml), "text/xml");
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                package.SanitizeMacros();

                using var stream = package.GetEntryStream("content.xml");
                var docNode = OdfXmlReader.Parse(stream);

                var rectNode = FindNode(docNode, "rect");
                Assert.NotNull(rectNode);
                Assert.Null(rectNode.GetAttribute("href", OdfNamespaces.XLink));

                var circleNode = FindNode(docNode, "circle");
                Assert.NotNull(circleNode);
                Assert.Null(circleNode.GetAttribute("href", OdfNamespaces.XLink));
            }
        }

        [Fact]
        public void TestDateParsingAndFormatting_IsCultureIndependent()
        {
            var originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
            var originalUICulture = System.Threading.Thread.CurrentThread.CurrentUICulture;
            try
            {
                var thaiCulture = new System.Globalization.CultureInfo("th-TH");
                System.Threading.Thread.CurrentThread.CurrentCulture = thaiCulture;
                System.Threading.Thread.CurrentThread.CurrentUICulture = thaiCulture;

                using var ms = new MemoryStream();
                using (var package = OdfPackage.Create(ms, leaveOpen: true))
                {
                    using (var doc = new TextDocument(package))
                    {
                        var testDate = new DateTime(2026, 6, 11, 8, 16, 35, DateTimeKind.Utc);
                        doc.CreationDate = testDate;

                        Assert.Equal(testDate, doc.CreationDate);

                        doc.SetCustomProperty("MyDate", testDate, "date");
                        var retrieved = doc.GetCustomProperty("MyDate");
                        Assert.Equal(testDate, retrieved);

                        doc.Save();
                    }
                }

                ms.Position = 0;
                using (var package = OdfPackage.Open(ms, leaveOpen: true))
                {
                    using var stream = package.GetEntryStream("meta.xml");
                    var metaNode = OdfXmlReader.Parse(stream);
                    var creationDateNode = FindNode(metaNode, "creation-date");
                    Assert.NotNull(creationDateNode);
                    Assert.Equal("2026-06-11T08:16:35Z", creationDateNode.TextContent);
                }
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = originalCulture;
                System.Threading.Thread.CurrentThread.CurrentUICulture = originalUICulture;
            }
        }

        [Fact]
        public void TestXmlCommentsPreserved()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");

                string contentXml =
                    "<office:document-content xmlns:office=\"" + OdfNamespaces.Office + "\">" +
                    "  <!-- start of body -->" +
                    "  <office:body>" +
                    "    <!-- inside body comment -->" +
                    "    <office:text>" +
                    "      <!-- inside text comment -->" +
                    "    </office:text>" +
                    "  </office:body>" +
                    "</office:document-content>";

                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(contentXml), "text/xml");
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                using var stream = package.GetEntryStream("content.xml");
                var docNode = OdfXmlReader.Parse(stream);

                var bodyNode = FindNode(docNode, "body");
                Assert.NotNull(bodyNode);

                OdfNode? startOfBodyComment = null;
                foreach (var child in docNode.Children)
                {
                    if (child.NodeType == OdfNodeType.Comment && child.TextContent.Trim() == "start of body")
                    {
                        startOfBodyComment = child;
                        break;
                    }
                }
                Assert.NotNull(startOfBodyComment);

                using var msOut = new MemoryStream();
                OdfXmlWriter.Write(docNode, msOut);

                string outputXml = Encoding.UTF8.GetString(msOut.ToArray());
                Assert.Contains("<!-- start of body -->", outputXml);
                Assert.Contains("<!-- inside body comment -->", outputXml);
                Assert.Contains("<!-- inside text comment -->", outputXml);
            }
        }

        private OdfNode? FindNode(OdfNode parent, string localName)
        {
            if (parent.LocalName == localName)
                return parent;
            foreach (var child in parent.Children)
            {
                var found = FindNode(child, localName);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
