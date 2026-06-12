using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Xunit;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Compliance;
using OdfKit.Text;
using OdfKit.Spreadsheet;
using OdfKit.Drawing;
using OdfKit.Styles;
using OdfKit.Presentation;

namespace OdfKit.Tests
{
    public class DomTest
    {
        [Fact]
        public void TestBasicDomParsingAndWriting()
        {
            string xml = @"<office:document-content xmlns:office=""urn:oasis:names:tc:opendocument:xmlns:office:1.0"" xmlns:text=""urn:oasis:names:tc:opendocument:xmlns:text:1.0"" office:version=""1.3"">
  <office:body>
    <office:text>
      <text:p text:style-name=""Standard"">Hello World</text:p>
    </office:text>
  </office:body>
</office:document-content>";

            // 1. Test Parse
            using var readStream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            OdfNode root = OdfXmlReader.Parse(readStream);

            Assert.Equal(OdfNodeType.Element, root.NodeType);
            Assert.Equal("document-content", root.LocalName);
            Assert.Equal(OdfNamespaces.Office, root.NamespaceUri);
            Assert.Equal("1.3", root.GetAttribute("version", OdfNamespaces.Office));

            // Verify children (filtering out formatting whitespace text nodes)
            OdfNode? FindElement(OdfNode parent, string name)
            {
                foreach (var child in parent.Children)
                {
                    if (child.NodeType == OdfNodeType.Element && child.LocalName == name)
                    {
                        return child;
                    }
                }
                return null;
            }

            OdfNode? body = FindElement(root, "body");
            Assert.NotNull(body);

            OdfNode? text = FindElement(body, "text");
            Assert.NotNull(text);

            OdfNode? paragraph = FindElement(text, "p");
            Assert.NotNull(paragraph);
            Assert.Equal(OdfNamespaces.Text, paragraph.NamespaceUri);
            Assert.Equal("Standard", paragraph.GetAttribute("style-name", OdfNamespaces.Text));
            Assert.Equal("Hello World", paragraph.TextContent);

            // 2. Test Modification
            paragraph.TextContent = "Hello OdfKit";
            Assert.Equal("Hello OdfKit", paragraph.TextContent);

            // 3. Test Write
            using var writeStream = new MemoryStream();
            var options = new OdfSaveOptions { IndentXml = true };
            OdfXmlWriter.Write(root, writeStream, options);

            string outputXml = Encoding.UTF8.GetString(writeStream.ToArray());
            Assert.Contains("Hello OdfKit", outputXml);
            Assert.Contains("xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"", outputXml);
        }

        [Fact]
        public void TestXmlDepthLimitDefense()
        {
            // Create a deeply nested XML document (300 levels deep)
            var sb = new StringBuilder();
            for (int i = 0; i < 300; i++)
            {
                sb.Append($"<node_{i}>");
            }
            sb.Append("Deep value");
            for (int i = 299; i >= 0; i--)
            {
                sb.Append($"</node_{i}>");
            }

            string xml = sb.ToString();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            // Parsing should throw a SecurityException because nesting depth 300 > MaxElementDepth 256
            var ex = Assert.Throws<SecurityException>(() => OdfXmlReader.Parse(stream));
            Assert.Contains("nesting depth limit exceeded", ex.Message);
        }

        [Fact]
        public void TestNodeCloningAndImport()
        {
            var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            pNode.SetAttribute("style-name", OdfNamespaces.Text, "Standard");
            pNode.TextContent = "Clone testing";

            // Test clone
            OdfNode clone = pNode.CloneNode(deep: true);
            Assert.Equal("p", clone.LocalName);
            Assert.Equal("Standard", clone.GetAttribute("style-name", OdfNamespaces.Text));
            Assert.Equal("Clone testing", clone.TextContent);
            Assert.Null(clone.Parent);

            // Test import node (local packages, no media migration needed)
            OdfNode imported = OdfNode.ImportNode(pNode, null, null);
            Assert.Equal("p", imported.LocalName);
            Assert.Equal("Standard", imported.GetAttribute("style-name", OdfNamespaces.Text));
            Assert.Equal("Clone testing", imported.TextContent);
        }

        [Fact]
        public void TestDigitalSignatures()
        {
            var logs = new List<string>();
            EventHandler<OdfDiagnosticsEventArgs> logHandler = (sender, e) => {
                string msg = $"[DIAGNOSTIC] {e.Level}: {e.Message} {(e.Exception != null ? e.Exception.ToString() : "")}";
                Console.WriteLine(msg);
                logs.Add(msg);
            };
            OdfKitDiagnostics.Log += logHandler;
            try
            {
                // 1. Create a dummy OdfPackage in memory with required entries
                using var ms = new MemoryStream();
                using (var package = OdfPackage.Create(ms, leaveOpen: true))
                {
                    package.SetMimeType("application/vnd.oasis.opendocument.text");
                    package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                    package.WriteEntry("styles.xml", Encoding.UTF8.GetBytes("<styles/>"), "text/xml");
                    package.Save();
                }

                // 2. Open the package for read-write
                ms.Position = 0;
                using (var package = OdfPackage.Open(ms, leaveOpen: true))
                {
                    // 3. Generate a self-signed certificate in-memory
                    using var rsa = RSA.Create(2048);
                    var req = new CertificateRequest("cn=OdfKitTest", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    using var cert = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(1));

                    // 4. Sign package
                    OdfSigner.Sign(package, cert);

                    Assert.True(package.HasEntry("META-INF/documentsignatures.xml"));

                    // 5. Verify signatures
                    bool isValid = OdfSigner.VerifySignatures(package, out var certs);
                    if (!isValid)
                    {
                        // Print documentsignatures.xml content
                        string sigXml = "";
                        try
                        {
                            using var s = package.GetEntryStream("META-INF/documentsignatures.xml");
                            using var sr = new StreamReader(s);
                            sigXml = sr.ReadToEnd();
                        }
                        catch {}
                        throw new Exception("Signature verification failed. Logs:\n" + string.Join("\n", logs) + "\nSignature XML:\n" + sigXml);
                    }
                    Assert.Single(certs);
                    Assert.Contains("CN=OdfKitTest", certs[0].Subject);

                    // 6. Save package
                    package.Save();
                }

                // 7. Verify signatures on reopened package
                ms.Position = 0;
                using (var package = OdfPackage.Open(ms, leaveOpen: true))
                {
                    bool isValid = OdfSigner.VerifySignatures(package, out var certs);
                    if (!isValid)
                    {
                        throw new Exception("Signature verification failed on reopened package. Logs:\n" + string.Join("\n", logs));
                    }
                }
            }
            finally
            {
                OdfKitDiagnostics.Log -= logHandler;
            }
        }

        [Fact]
        public void TestTypedDomWrappers()
        {
            string xml = @"<office:document-content xmlns:office=""urn:oasis:names:tc:opendocument:xmlns:office:1.0"" xmlns:text=""urn:oasis:names:tc:opendocument:xmlns:text:1.0"" xmlns:table=""urn:oasis:names:tc:opendocument:xmlns:table:1.0"" office:version=""1.3"">
  <office:body>
    <office:text>
      <text:h text:style-name=""Heading1"" text:outline-level=""2"">My Heading</text:h>
      <text:p text:style-name=""Standard"">Hello World</text:p>
      <table:table table:name=""Sheet1"">
        <table:table-row>
          <table:table-cell office:value-type=""string"">Cell Value</table:table-cell>
        </table:table-row>
      </table:table>
    </office:text>
  </office:body>
</office:document-content>";

            using var readStream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            OdfNode root = OdfXmlReader.Parse(readStream);

            Assert.IsType<OfficeDocumentContentElement>(root);

            OdfNode? FindElement(OdfNode parent, string name)
            {
                foreach (var child in parent.Children)
                {
                    if (child.NodeType == OdfNodeType.Element && child.LocalName == name)
                    {
                        return child;
                    }
                }
                return null;
            }

            var body = FindElement(root, "body");
            Assert.IsType<OfficeBodyElement>(body);

            var text = FindElement(body!, "text");
            Assert.IsType<OfficeTextElement>(text);

            var heading = FindElement(text!, "h");
            var headingElement = Assert.IsType<TextHElement>(heading);
            Assert.Equal("Heading1", headingElement.StyleName);
            Assert.Equal(2, headingElement.OutlineLevel);

            var paragraph = FindElement(text!, "p");
            var pElement = Assert.IsType<TextPElement>(paragraph);
            Assert.Equal("Standard", pElement.StyleName);

            var table = FindElement(text!, "table");
            var tableElement = Assert.IsType<TableTableElement>(table);
            Assert.Equal("Sheet1", tableElement.Name);

            // Test setting properties
            headingElement.OutlineLevel = 3;
            Assert.Equal("3", headingElement.GetAttribute("outline-level", OdfNamespaces.Text));
            Assert.Equal(3, headingElement.OutlineLevel);

            pElement.StyleName = "NewStyle";
            Assert.Equal("NewStyle", pElement.GetAttribute("style-name", OdfNamespaces.Text));

            // Test Cloning preserves types
            var headingClone = headingElement.CloneNode(deep: true);
            Assert.IsType<TextHElement>(headingClone);
            Assert.Equal("Heading1", ((TextHElement)headingClone).StyleName);
            Assert.Equal(3, ((TextHElement)headingClone).OutlineLevel);
        }

        [Fact]
        public void TestDocumentEnhancements()
        {
            OdfNode? FindDescendant(OdfNode parent, string localName, string namespaceUri)
            {
                foreach (var child in parent.Children)
                {
                    if (child.NodeType == OdfNodeType.Element && child.LocalName == localName && child.NamespaceUri == namespaceUri)
                    {
                        return child;
                    }
                    var found = FindDescendant(child, localName, namespaceUri);
                    if (found != null) return found;
                }
                return null;
            }

            // 1. TextDocument Enhancements
            using (var package = OdfPackage.Create(new MemoryStream()))
            {
                var doc = new TextDocument(package);
                
                // AddHeading
                var heading = doc.AddHeading("Introduction", 1);
                Assert.NotNull(heading);
                Assert.Equal("Introduction", heading.Node.TextContent);
                Assert.Equal("1", heading.Node.GetAttribute("outline-level", OdfNamespaces.Text));

                // AddList
                var list = doc.AddList("ListStyle");
                Assert.NotNull(list);
                Assert.Equal("ListStyle", list.Node.GetAttribute("style-name", OdfNamespaces.Text));

                // Add Paragraph and Fields
                var p = doc.AddParagraph("Some paragraph");
                doc.AddDateField(p);
                doc.AddTimeField(p);
                doc.AddAuthorField(p);
                doc.AddChapterField(p);
                doc.AddSequenceField(p, "Illustration", "1");
                doc.AddReferenceField(p, "ref1");
                doc.AddVariableSetField(p, "myVar", "100");
                doc.AddVariableGetField(p, "myVar");

                Assert.NotNull(FindDescendant(p.Node, "date", OdfNamespaces.Text));
                Assert.NotNull(FindDescendant(p.Node, "time", OdfNamespaces.Text));
                Assert.NotNull(FindDescendant(p.Node, "author-name", OdfNamespaces.Text));
                Assert.NotNull(FindDescendant(p.Node, "chapter", OdfNamespaces.Text));
                
                var seq = FindDescendant(p.Node, "sequence", OdfNamespaces.Text);
                Assert.NotNull(seq);
                Assert.Equal("Illustration", seq.GetAttribute("name", OdfNamespaces.Text));

                var refRef = FindDescendant(p.Node, "reference-ref", OdfNamespaces.Text);
                Assert.NotNull(refRef);
                Assert.Equal("ref1", refRef.GetAttribute("ref-name", OdfNamespaces.Text));

                var varSet = FindDescendant(p.Node, "variable-set", OdfNamespaces.Text);
                Assert.NotNull(varSet);
                Assert.Equal("myVar", varSet.GetAttribute("name", OdfNamespaces.Text));

                var varGet = FindDescendant(p.Node, "variable-get", OdfNamespaces.Text);
                Assert.NotNull(varGet);
                Assert.Equal("myVar", varGet.GetAttribute("name", OdfNamespaces.Text));

                // Indexes
                doc.AddAlphabeticalIndex();
                doc.AddBibliography();
                doc.AddTableIndex();
                Assert.NotNull(FindDescendant(doc.ContentRoot, "alphabetical-index", OdfNamespaces.Text));
                Assert.NotNull(FindDescendant(doc.ContentRoot, "bibliography", OdfNamespaces.Text));
                Assert.NotNull(FindDescendant(doc.ContentRoot, "table-index", OdfNamespaces.Text));

                // Comments & bookmarks & hyperlink & Ruby & Image
                doc.AddCommentStart(p, "comment1");
                doc.AddCommentEnd(p, "comment1");
                doc.AddBookmark(p, "bookmark1");
                doc.AddReferenceMark(p, "refmark1");
                doc.AddHyperlink(p, "http://example.com", "link");
                
                var img = doc.AddImage(p, "Pictures/test.png", "5cm", "4cm", "img1");
                Assert.NotNull(img);
                
                doc.AddRuby(p, "base", "ruby");

                var annStart = FindDescendant(p.Node, "annotation-start", OdfNamespaces.Office);
                Assert.NotNull(annStart);
                Assert.Equal("comment1", annStart.GetAttribute("name", OdfNamespaces.Office));

                var annEnd = FindDescendant(p.Node, "annotation-end", OdfNamespaces.Office);
                Assert.NotNull(annEnd);
                Assert.Equal("comment1", annEnd.GetAttribute("name", OdfNamespaces.Office));

                var bmark = FindDescendant(p.Node, "bookmark", OdfNamespaces.Text);
                Assert.NotNull(bmark);
                Assert.Equal("bookmark1", bmark.GetAttribute("name", OdfNamespaces.Text));

                var rmark = FindDescendant(p.Node, "reference-mark", OdfNamespaces.Text);
                Assert.NotNull(rmark);
                Assert.Equal("refmark1", rmark.GetAttribute("name", OdfNamespaces.Text));

                var hyperlink = FindDescendant(p.Node, "a", OdfNamespaces.Text);
                Assert.NotNull(hyperlink);
                Assert.Equal("http://example.com", hyperlink.GetAttribute("href", OdfNamespaces.XLink));

                var frame = FindDescendant(p.Node, "frame", OdfNamespaces.Draw);
                Assert.NotNull(frame);
                Assert.Equal("img1", frame.GetAttribute("name", OdfNamespaces.Draw));

                Assert.NotNull(FindDescendant(p.Node, "ruby", OdfNamespaces.Text));

                // Tracked Changes
                var tcNode = OdfNodeFactory.CreateElement("tracked-changes", OdfNamespaces.Text, "text");
                var changedRegion = OdfNodeFactory.CreateElement("changed-region", OdfNamespaces.Text, "text");
                changedRegion.SetAttribute("id", OdfNamespaces.Text, "change1", "text");
                var insertion = OdfNodeFactory.CreateElement("insertion", OdfNamespaces.Text, "text");
                changedRegion.AppendChild(insertion);
                tcNode.AppendChild(changedRegion);
                doc.BodyTextRoot.AppendChild(tcNode);

                var changeStart = OdfNodeFactory.CreateElement("change-start", OdfNamespaces.Text, "text");
                changeStart.SetAttribute("change-id", OdfNamespaces.Text, "change1", "text");
                p.Node.AppendChild(changeStart);
                var changeTextNode = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "Inserted Content" };
                p.Node.AppendChild(changeTextNode);
                var changeEnd = OdfNodeFactory.CreateElement("change-end", OdfNamespaces.Text, "text");
                changeEnd.SetAttribute("change-id", OdfNamespaces.Text, "change1", "text");
                p.Node.AppendChild(changeEnd);

                doc.RejectChange("change1");
                Assert.Null(FindDescendant(p.Node, "change-start", OdfNamespaces.Text));
                Assert.DoesNotContain("Inserted Content", p.Node.TextContent);
            }

            // 2. SpreadsheetDocument Password Verification
            using (var package = OdfPackage.Create(new MemoryStream()))
            {
                var doc = new SpreadsheetDocument(package);
                var sheet = doc.AddSheet("Sheet1");

                sheet.Protect("secret");
                Assert.True(sheet.IsProtected);
                Assert.True(sheet.VerifyPassword("secret"));
                Assert.False(sheet.VerifyPassword("wrong"));

                doc.ProtectWorkbook("wbsecret");
                Assert.True(doc.WorkbookStructureProtected);
                Assert.True(doc.VerifyWorkbookPassword("wbsecret"));
                Assert.False(doc.VerifyWorkbookPassword("wrong"));
            }

            // 3. DrawingDocument Enhancements
            using (var package = OdfPackage.Create(new MemoryStream()))
            {
                var doc = new OdfKit.Drawing.DrawingDocument(package);
                var page = doc.AddPage("Page1");
                Assert.NotNull(page);
                Assert.Equal("Page1", page.Name);

                var shape = page.AddShape(OdfShapeType.Rectangle, new OdfLength(10, OdfUnit.Millimeters), new OdfLength(20, OdfUnit.Millimeters), new OdfLength(30, OdfUnit.Millimeters), new OdfLength(40, OdfUnit.Millimeters));
                Assert.NotNull(shape);
                Assert.Equal("rect", shape.Node.LocalName);
                Assert.Equal("10mm", shape.Node.GetAttribute("x", OdfNamespaces.Svg));

                var textBox = page.AddTextBox(new OdfLength(10, OdfUnit.Millimeters), new OdfLength(20, OdfUnit.Millimeters), new OdfLength(30, OdfUnit.Millimeters), new OdfLength(40, OdfUnit.Millimeters), "Hello Shape");
                Assert.NotNull(textBox);
                Assert.Equal("Hello Shape", textBox.Node.TextContent);
            }
        }

        [Fact]
        public void TestTextDocumentMathMLAndTOCRoundTrip()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new TextDocument(package);
                var p = doc.AddParagraph("Formula paragraph:");
                doc.AddFormula(p, "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>x</mi></math>");
                doc.AddTableOfContents();
                doc.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                var doc = new TextDocument(package);
                
                // Verify MathML frame and object exists
                var bodyText = doc.BodyTextRoot;
                Assert.NotNull(bodyText);
                
                OdfNode? frame = null;
                foreach (var child in bodyText.Children)
                {
                    if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
                    {
                        foreach (var inner in child.Children)
                        {
                            if (inner.LocalName == "frame" && inner.NamespaceUri == OdfNamespaces.Draw)
                            {
                                frame = inner;
                                break;
                            }
                        }
                    }
                }
                Assert.NotNull(frame);
                OdfNode? obj = null;
                foreach (var child in frame.Children)
                {
                    if (child.LocalName == "object" && child.NamespaceUri == OdfNamespaces.Draw)
                    {
                        obj = child;
                        break;
                    }
                }
                Assert.NotNull(obj);
                string? href = obj.GetAttribute("href", OdfNamespaces.XLink);
                Assert.NotNull(href);

                // Verify embedded formula content.xml exists and is preserved
                Assert.True(package.HasEntry($"{href}/content.xml"));
                Assert.True(package.HasEntry($"{href}/mimetype"));
                Assert.Equal("application/vnd.oasis.opendocument.formula", package.Manifest[$"{href}/mimetype"]);

                // Verify TOC exists
                OdfNode? toc = null;
                foreach (var child in bodyText.Children)
                {
                    if (child.LocalName == "table-of-content" && child.NamespaceUri == OdfNamespaces.Text)
                    {
                        toc = child;
                        break;
                    }
                }
                Assert.NotNull(toc);
                Assert.Equal("Table of Contents", toc.GetAttribute("name", OdfNamespaces.Text));
            }
        }

        [Fact]
        public void TestVersionAwareTypedDomWrappersAndRoundTrip()
        {
            // 1. Parent walk-up version resolution
            var p = new TextPElement("text");
            Assert.Equal(OdfVersion.Odf14, p.GetDocumentVersion());

            var docContent = new OfficeDocumentContentElement("office");
            docContent.AppendChild(p);
            Assert.Equal(OdfVersion.Odf14, p.GetDocumentVersion());

            docContent.SetAttribute("version", OdfNamespaces.Office, "1.2");
            Assert.Equal(OdfVersion.Odf12, p.GetDocumentVersion());

            docContent.SetAttribute("version", OdfNamespaces.Office, "1.3");
            Assert.Equal(OdfVersion.Odf13, p.GetDocumentVersion());

            docContent.RemoveAttribute("version", OdfNamespaces.Office);
            Assert.Equal(OdfVersion.Odf14, p.GetDocumentVersion());

            // 2. Nullable property cleanup deletes attribute
            p.StyleName = "MyStyle";
            Assert.Equal("MyStyle", p.StyleName);
            Assert.Equal("MyStyle", p.GetAttribute("style-name", OdfNamespaces.Text));

            p.StyleName = null;
            Assert.Null(p.StyleName);
            Assert.Null(p.GetAttribute("style-name", OdfNamespaces.Text));
            Assert.False(p.Attributes.ContainsKey(new OdfAttributeName("style-name", OdfNamespaces.Text)));

            // 3. Round-trip preservation of unknown/foreign attributes
            var foreignAttr = new OdfAttributeName("custom-attr", "http://example.com/custom");
            p.SetAttribute("custom-attr", "http://example.com/custom", "custom-value", "custom");
            p.StyleName = "SomeStyle";
            Assert.Equal("custom-value", p.GetAttribute("custom-attr", "http://example.com/custom"));
            Assert.Equal("SomeStyle", p.StyleName);

            // 4. Version-aware warnings checking
            bool warningTriggered = false;
            string? warningMessage = null;
            EventHandler<OdfDiagnosticsEventArgs> logHandler = (sender, args) =>
            {
                if (args.Level == OdfDiagnosticsLevel.Warning)
                {
                    warningTriggered = true;
                    warningMessage = args.Message;
                }
            };

            OdfKitDiagnostics.Log += logHandler;
            try
            {
                docContent.SetAttribute("version", OdfNamespaces.Office, "1.1");
                p.SetAttributeValue("non-existent-attr", "urn:oasis:names:tc:opendocument:xmlns:text:1.0", "val", "text", p.GetDocumentVersion());
                Assert.True(warningTriggered);
                Assert.Contains("non-existent-attr", warningMessage);
            }
            finally
            {
                OdfKitDiagnostics.Log -= logHandler;
            }
        }
    }
}
