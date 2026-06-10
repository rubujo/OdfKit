using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Xunit;
using OdfKit.Core;
using OdfKit.DOM;

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
    }
}
