using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests
{
    [Trait(TestCategories.Kind, TestCategories.Corpus)]
    [Trait(TestCategories.Kind, TestCategories.Compliance)]
    public class CorpusComplianceTests
    {
        private static void LogReport(string testName, OdfValidationReport report)
        {
            _ = testName;
            _ = report;
        }

        private static MemoryStream CreatePackage(
            string mimeType,
            string contentXml,
            IEnumerable<KeyValuePair<string, string>>? additionalXmlEntries = null)
        {
            var ms = new MemoryStream();
            using (OdfPackage package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType(mimeType);
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(contentXml), "text/xml");
                if (additionalXmlEntries != null)
                {
                    foreach (var entry in additionalXmlEntries)
                    {
                        package.WriteEntry(entry.Key, Encoding.UTF8.GetBytes(entry.Value), "text/xml");
                    }
                }
                package.Save();
            }
            ms.Position = 0;
            return ms;
        }

        private static MemoryStream CreateZipWithCustomEntry(string entryName, byte[] data)
        {
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                ZipArchiveEntry mimetype = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
                using (Stream stream = mimetype.Open())
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.Write("application/vnd.oasis.opendocument.text");
                }

                ZipArchiveEntry content = zip.CreateEntry("content.xml", CompressionLevel.Fastest);
                using (Stream stream = content.Open())
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.Write("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:body><office:text/></office:body></office:document-content>");
                }

                ZipArchiveEntry custom = zip.CreateEntry(entryName, CompressionLevel.Fastest);
                using (Stream stream = custom.Open())
                {
                    stream.Write(data, 0, data.Length);
                }

                ZipArchiveEntry manifest = zip.CreateEntry("META-INF/manifest.xml", CompressionLevel.Fastest);
                using (Stream stream = manifest.Open())
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.Write("<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.2\">" +
                                 "<manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.text\"/>" +
                                 "<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/>" +
                                 $"<manifest:file-entry manifest:full-path=\"{entryName}\" manifest:media-type=\"application/octet-stream\"/>" +
                                 "</manifest:manifest>");
                }
            }
            ms.Position = 0;
            return ms;
        }

        // 1. OASIS ODF 1.4 Strict Tests
        [Fact]
        public void OasisOdf14Strict_Positive()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.4\"><office:body><office:text><text:p>Hello World</text:p></office:text></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);
            LogReport("OasisOdf14Strict_Positive", report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
        }

        [Fact]
        public void OasisOdf14Strict_Negative_VersionMismatch()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.2\"><office:body><office:text><text:p>Hello World</text:p></office:text></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict, "test.odt");
            LogReport("OasisOdf14Strict_Negative_VersionMismatch", report);
            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF1001" && issue.PackagePath == "test.odt" && issue.XPath == "/office:document-content[1]");
        }

        [Fact]
        public void OasisOdf14Strict_Negative_NamespaceExtension()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.4\"><office:body><office:text><text:non-existent-element /></office:text></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict, "test.odt");
            LogReport("OasisOdf14Strict_Negative_NamespaceExtension", report);
            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue => issue.RuleId == "DisallowInvalidOdfNamespaceExtensions" && issue.XPath == "/office:document-content[1]/office:body[1]/office:text[1]/text:non-existent-element[1]" && issue.RequiredVersion == null);
        }

        // 2. OASIS ODF 1.4 Extended Tests
        [Fact]
        public void OasisOdf14Extended_Positive()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:ext=\"urn:foreign:extension\" office:version=\"1.4\"><office:body><office:text><ext:foreign-el /></office:text></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Extended);
            LogReport("OasisOdf14Extended_Positive", report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
        }

        [Fact]
        public void OasisOdf14Extended_Negative_InvalidOdfExtension()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.4\"><office:body><office:text><text:non-existent-element /></office:text></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Extended, "test.odt");
            LogReport("OasisOdf14Extended_Negative_InvalidOdfExtension", report);
            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue => issue.RuleId == "RequireOdfNamespaceValidity" && issue.XPath == "/office:document-content[1]/office:body[1]/office:text[1]/text:non-existent-element[1]");
        }

        // 3. ISO/IEC 26300 Tests
        [Fact]
        public void IsoIec26300_Positive()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.2\"><office:body><office:text/></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.IsoIec26300_2015);
            LogReport("IsoIec26300_Positive", report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
        }

        [Fact]
        public void IsoIec26300_Negative_VersionMismatch()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:body><office:text/></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.IsoIec26300_2015, "test.odt");
            LogReport("IsoIec26300_Negative_VersionMismatch", report);
            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF1001" && issue.PackagePath == "test.odt" && issue.XPath == "/office:document-content[1]");
        }

        // 4. EU Interoperable Europe Tests
        [Fact]
        public void EuInteroperableEurope_Positive()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" office:version=\"1.4\"><office:body><office:text>" +
                             "<draw:frame><draw:image><svg:title>A nice image</svg:title></draw:image></draw:frame>" +
                             "<table:table><table:table-header-rows/></table:table>" +
                             "</office:text></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.EuInteroperableEurope);
            LogReport("EuInteroperableEurope_Positive", report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
            Assert.DoesNotContain(report.Issues, i => i.RuleId == "RequireAccessibilityMetadata");
        }

        [Fact]
        public void EuInteroperableEurope_Negative_AltText()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" office:version=\"1.4\"><office:body><office:text>" +
                             "<draw:frame><draw:image/></draw:frame>" +
                             "</office:text></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.EuInteroperableEurope);
            LogReport("EuInteroperableEurope_Negative_AltText", report);
            Assert.True(report.IsValid); // Warning only
            Assert.Contains(report.Issues, issue => issue.RuleId == "RequireAccessibilityMetadata" && issue.XPath == "/office:document-content[1]/office:body[1]/office:text[1]/draw:frame[1]/draw:image[1]");
        }

        [Fact]
        public void EuInteroperableEurope_Negative_TableHeaders()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" office:version=\"1.4\"><office:body><office:text>" +
                             "<table:table><table:table-row/></table:table>" +
                             "</office:text></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.EuInteroperableEurope);
            LogReport("EuInteroperableEurope_Negative_TableHeaders", report);
            Assert.True(report.IsValid); // Warning only
            Assert.Contains(report.Issues, issue => issue.RuleId == "RequireAccessibilityMetadata" && issue.XPath == "/office:document-content[1]/office:body[1]/office:text[1]/table:table[1]");
        }

        [Fact]
        public void EuInteroperableEurope_Negative_ExternalLink()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" office:version=\"1.4\"><office:body><office:text>" +
                             "<draw:image xlink:href=\"http://example.com/image.png\"/>" +
                             "</office:text></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.EuInteroperableEurope);
            LogReport("EuInteroperableEurope_Negative_ExternalLink", report);
            Assert.True(report.IsValid); // Warning only
            Assert.Contains(report.Issues, issue => issue.RuleId == "RequireCrossBorderInteroperability");
        }

        // 5. EU Office Document Exchange Tests
        [Fact]
        public void EuOfficeDocumentExchange_Positive()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:body><office:text/></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.EuOfficeDocumentExchange);
            LogReport("EuOfficeDocumentExchange_Positive", report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
        }

        [Fact]
        public void EuOfficeDocumentExchange_Negative_ExternalLink()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" office:version=\"1.4\"><office:body><office:text>" +
                             "<draw:image xlink:href=\"https://example.com/image.png\"/>" +
                             "</office:text></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.EuOfficeDocumentExchange);
            LogReport("EuOfficeDocumentExchange_Negative_ExternalLink", report);
            Assert.True(report.IsValid); // Warning only
            Assert.Contains(report.Issues, issue => issue.RuleId == "RequireCrossBorderInteroperability");
        }

        // 6. ROC Taiwan ODF CNS15251 Tests
        [Fact]
        public void RocTaiwanOdfCns15251_Positive()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.2\"><office:body><office:text/></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.RocTaiwanOdfCns15251);
            LogReport("RocTaiwanOdfCns15251_Positive", report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
        }

        [Fact]
        public void RocTaiwanOdfCns15251_Negative_ZipSlip()
        {
            byte[] dummyData = Encoding.UTF8.GetBytes("<root/>");
            using MemoryStream ms = CreateZipWithCustomEntry("../illegal.xml", dummyData);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.RocTaiwanOdfCns15251);
            LogReport("RocTaiwanOdfCns15251_Negative_ZipSlip", report);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF0200" &&
                issue.PackagePath == "../illegal.xml" &&
                issue.Severity == OdfIssueSeverity.Fatal);
        }

        [Fact]
        public void RocTaiwanOdfCns15251_Negative_MacroEntry()
        {
            byte[] dummyData = Encoding.UTF8.GetBytes("Sub Main\nEnd Sub");
            using MemoryStream ms = CreateZipWithCustomEntry("Basic/script.xlb", dummyData);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.RocTaiwanOdfCns15251);
            LogReport("RocTaiwanOdfCns15251_Negative_MacroEntry", report);
            Assert.True(report.IsValid); // Warning only
            Assert.Contains(report.Issues, issue => issue.RuleId == "DisallowMacroByDefault" && issue.PackagePath == "Basic/script.xlb");
        }

        [Fact]
        public void RocTaiwanOdfCns15251_Negative_ScriptAttribute()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:script=\"urn:oasis:names:tc:opendocument:xmlns:script:1.0\" office:version=\"1.2\">" +
                             "<office:scripts><office:event-listeners><script:event-listener script:event-name=\"dom-click\" script:language=\"ooo:script\" script:macro-name=\"MyMacro\" /></office:event-listeners></office:scripts>" +
                             "<office:body><office:text/></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.RocTaiwanOdfCns15251);
            LogReport("RocTaiwanOdfCns15251_Negative_ScriptAttribute", report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.RuleId + ": " + i.Message))); // Warning only
            Assert.Contains(report.Issues, issue => issue.RuleId == "DisallowMacroByDefault" && issue.XPath == "/office:document-content[1]/office:scripts[1]/office:event-listeners[1]/script:event-listener[1]");
        }

        // 7. ROC Taiwan Government ODF Tools Tests
        [Fact]
        public void RocTaiwanGovernmentOdfTools_Positive()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.2\"><office:body><office:text/></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.RocTaiwanGovernmentOdfTools);
            LogReport("RocTaiwanGovernmentOdfTools_Positive", report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
        }

        [Fact]
        public void RocTaiwanGovernmentOdfTools_Negative_ScriptValue()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:script=\"urn:oasis:names:tc:opendocument:xmlns:script:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" office:version=\"1.2\">" +
                             "<office:scripts><office:event-listeners><script:event-listener script:event-name=\"dom-click\" script:language=\"ooo:script\" xlink:type=\"simple\" xlink:href=\"vnd.sun.star.script:MyMacro\" /></office:event-listeners></office:scripts>" +
                             "<office:body><office:text/></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.RocTaiwanGovernmentOdfTools);
            LogReport("RocTaiwanGovernmentOdfTools_Negative_ScriptValue", report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.RuleId + ": " + i.Message))); // Warning only
            Assert.Contains(report.Issues, issue => issue.RuleId == "DisallowMacroByDefault");
        }

        [Theory]
        [InlineData("draw:image", "https://example.invalid/remote-image.png")]
        [InlineData("draw:object", "https://example.invalid/remote-object.odg")]
        [InlineData("draw:plugin", "//cdn.example.invalid/plugin.bin")]
        public void RocTaiwanGovernmentOdfTools_Negative_RemoteResourceReferences(
            string elementName,
            string href)
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" office:version=\"1.2\">" +
                             "<office:body><office:text><draw:frame>" +
                             $"<{elementName} xlink:type=\"simple\" xlink:href=\"{href}\" />" +
                             "</draw:frame></office:text></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(
                package,
                OdfComplianceProfiles.RocTaiwanGovernmentOdfTools,
                "remote-resource.odt");

            LogReport("RocTaiwanGovernmentOdfTools_Negative_RemoteResourceReferences", report);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "RequireSafeExternalResourcePolicy" &&
                issue.PackagePath == "content.xml" &&
                issue.Message.Contains(href, StringComparison.Ordinal));
        }

        // Cross-Version Schema Lookup test
        [Fact]
        public void CrossVersionSchemaLookup_DetectsCorrectRequiredVersion()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.2\"><office:body><office:text><text:custom-new-element /></office:text></office:body></office:document-content>";

            var customElement = new OdfElementDefinition(
                new OdfQualifiedName(OdfNamespaces.Text, "custom-new-element"),
                OdfSchemaElementRole.Element,
                OdfVersionRange.AllKnown);

            var schema13 = new OdfSchemaSet(
                OdfVersion.Odf13,
                new Uri("https://example.invalid/custom.rng"),
                "2026-06-12",
                new[] { customElement },
                Array.Empty<OdfAttributeDefinition>());

            using (OdfSchemaRegistry.RegisterSchema(schema13, mergeWithExisting: true, overwriteExisting: true))
            using (MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content))
            using (OdfPackage package = OdfPackage.Open(ms))
            {
                OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.IsoIec26300_2015);
                LogReport("CrossVersionSchemaLookup_DetectsCorrectRequiredVersion", report);
                Assert.False(report.IsValid);
                Assert.Contains(report.Issues, issue => issue.RuleId == "RequireOdfNamespaceValidity" &&
                                                        issue.RequiredVersion == OdfVersion.Odf13 &&
                                                        issue.XPath == "/office:document-content[1]/office:body[1]/office:text[1]/text:custom-new-element[1]");
            }
        }

        [Fact]
        public void OasisOdf14Corpus_Positive_DocumentStyles()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:body><office:text/></office:body></office:document-content>";
            var additional = new Dictionary<string, string>
            {
                { "styles.xml", "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" office:version=\"1.4\"><office:styles/></office:document-styles>" }
            };
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content, additional);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);
            LogReport("OasisOdf14Corpus_Positive_DocumentStyles", report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
        }

        [Fact]
        public void OasisOdf14Corpus_Positive_DocumentMeta()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:body><office:text/></office:body></office:document-content>";
            var additional = new Dictionary<string, string>
            {
                { "meta.xml", "<office:document-meta xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" office:version=\"1.4\"><office:meta><meta:generator>OdfKit</meta:generator></office:meta></office:document-meta>" }
            };
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content, additional);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);
            LogReport("OasisOdf14Corpus_Positive_DocumentMeta", report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
        }

        [Fact]
        public void OasisOdf14Corpus_Positive_DocumentSettings()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:body><office:text/></office:body></office:document-content>";
            var additional = new Dictionary<string, string>
            {
                { "settings.xml", "<office:document-settings xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"1.4\"><office:settings><config:config-item-set config:name=\"ooo:view-settings\"><config:config-item config:name=\"ShowGrid\" config:type=\"boolean\">true</config:config-item></config:config-item-set></office:settings></office:document-settings>" }
            };
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content, additional);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);
            LogReport("OasisOdf14Corpus_Positive_DocumentSettings", report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
        }

        [Fact]
        public void OasisOdf14Corpus_Positive_DocumentContent()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.4\"><office:body><office:text><text:p>ODF 1.4 positive corpus</text:p></office:text></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);
            LogReport("OasisOdf14Corpus_Positive_DocumentContent", report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
        }

        [Fact]
        public void OasisOdf14Corpus_Positive_DocumentContentWithOptionalOfficeBlocks()
        {
            string content =
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
                "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.4\">" +
                "<office:font-face-decls/>" +
                "<office:automatic-styles/>" +
                "<office:body><office:text><text:p>Official content model order</text:p></office:text></office:body>" +
                "</office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            LogReport("OasisOdf14Corpus_Positive_DocumentContentWithOptionalOfficeBlocks", report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
        }

        [Theory]
        [InlineData(OdfDocumentKind.Text, "document.odt")]
        [InlineData(OdfDocumentKind.Spreadsheet, "workbook.ods")]
        [InlineData(OdfDocumentKind.Presentation, "slides.odp")]
        [InlineData(OdfDocumentKind.Graphics, "drawing.odg")]
        [InlineData(OdfDocumentKind.Chart, "chart.odc")]
        [InlineData(OdfDocumentKind.Formula, "formula.odf")]
        [InlineData(OdfDocumentKind.Image, "image.odi")]
        [InlineData(OdfDocumentKind.Database, "database.odb")]
        public void OasisOdf14Corpus_Positive_AllPackageBodyKinds(OdfDocumentKind kind, string fileName)
        {
            using var ms = new MemoryStream();
            using (OdfPackage package = OdfDocumentFactory.CreatePackage(ms, kind, leaveOpen: true))
            {
                package.Save();
            }

            ms.Position = 0;
            using OdfPackage reopened = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(
                reopened,
                OdfComplianceProfiles.OasisOdf14Strict,
                fileName);

            LogReport("OasisOdf14Corpus_Positive_AllPackageBodyKinds_" + kind, report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
        }

        [Fact]
        public void OasisOdf14Corpus_Positive_FlatDocument()
        {
            string flatXml = "<office:document xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"1.4\" office:mimetype=\"application/vnd.oasis.opendocument.text\"><office:meta/><office:settings><config:config-item-set config:name=\"ooo:view-settings\"><config:config-item config:name=\"ShowGrid\" config:type=\"boolean\">true</config:config-item></config:config-item-set></office:settings><office:styles/><office:body><office:text/></office:body></office:document>";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(flatXml));
            OdfValidationReport report = OdfFlatDocumentValidator.Validate(ms, "document.fodt", OdfComplianceProfiles.OasisOdf14Strict);
            LogReport("OasisOdf14Corpus_Positive_FlatDocument", report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
        }

        [Theory]
        [InlineData(OdfDocumentKind.FlatText, "document.fodt")]
        [InlineData(OdfDocumentKind.FlatSpreadsheet, "workbook.fods")]
        [InlineData(OdfDocumentKind.FlatPresentation, "slides.fodp")]
        [InlineData(OdfDocumentKind.FlatGraphics, "drawing.fodg")]
        public void OasisOdf14Corpus_Positive_AllFlatBodyKinds(OdfDocumentKind kind, string fileName)
        {
            using var ms = new MemoryStream();
            OdfDocumentFactory.WriteFlatXml(ms, kind, leaveOpen: true);
            ms.Position = 0;

            OdfValidationReport report = OdfFlatDocumentValidator.Validate(
                ms,
                fileName,
                OdfComplianceProfiles.OasisOdf14Strict);

            LogReport("OasisOdf14Corpus_Positive_AllFlatBodyKinds_" + kind, report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
        }

        [Fact]
        public void OasisOdf14Corpus_Negative_MissingVersion()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"><office:body><office:text/></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);
            LogReport("OasisOdf14Corpus_Negative_MissingVersion", report);
            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF0400");
        }

        [Fact]
        public void OasisOdf14Corpus_Negative_InvalidRoot()
        {
            string content = "<office:unknown-root xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:body><office:text/></office:body></office:unknown-root>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);
            LogReport("OasisOdf14Corpus_Negative_InvalidRoot", report);
            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF0300" &&
                issue.PackagePath == "content.xml");
        }

        [Fact]
        public void OasisOdf14Corpus_Negative_InvalidBodyKind()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:body><office:unknown-body/></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);
            LogReport("OasisOdf14Corpus_Negative_InvalidBodyKind", report);
            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF3002" &&
                issue.PackagePath == "content.xml");
        }

        [Fact]
        public void OasisOdf14Corpus_Negative_InvalidAttributeDatatype()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" office:version=\"1.4\"><office:body><office:text><table:table table:name=\"Sheet1\"><table:table-column table:number-columns-repeated=\"not-a-number\"/><table:table-row><table:table-cell office:value-type=\"string\" office:string-value=\"A1\"/></table:table-row></table:table></office:text></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);
            LogReport("OasisOdf14Corpus_Negative_InvalidAttributeDatatype", report);
            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF3101" &&
                issue.PackagePath == "content.xml");
        }

        [Fact]
        public void OasisOdf14Corpus_Positive_TableRowChoiceCoveredCell()
        {
            string content =
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
                "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" office:version=\"1.4\">" +
                "<office:body><office:spreadsheet><table:table table:name=\"Sheet1\">" +
                "<table:table-column/>" +
                "<table:table-row><table:covered-table-cell/></table:table-row>" +
                "</table:table></office:spreadsheet></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.spreadsheet", content);
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            LogReport("OasisOdf14Corpus_Positive_TableRowChoiceCoveredCell", report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
        }

        [Fact]
        public void OasisOdf14Corpus_Negative_TableRowRequiresAtLeastOneCell()
        {
            string content =
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
                "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" office:version=\"1.4\">" +
                "<office:body><office:spreadsheet><table:table table:name=\"Sheet1\">" +
                "<table:table-column/>" +
                "<table:table-row/>" +
                "</table:table></office:spreadsheet></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.spreadsheet", content);
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            LogReport("OasisOdf14Corpus_Negative_TableRowRequiresAtLeastOneCell", report);
            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF3101" &&
                issue.PackagePath == "content.xml");
        }

        [Fact]
        public void OasisOdf14Corpus_Positive_PageLayoutPropertiesInterleaveOutOfOrder()
        {
            string content =
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
                "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
                "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" office:version=\"1.4\">" +
                "<office:automatic-styles><style:page-layout style:name=\"pm1\">" +
                "<style:page-layout-properties>" +
                "<style:footnote-sep/>" +
                "<style:columns fo:column-count=\"2\"/>" +
                "</style:page-layout-properties>" +
                "</style:page-layout></office:automatic-styles>" +
                "<office:body><office:text/></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            LogReport("OasisOdf14Corpus_Positive_PageLayoutPropertiesInterleaveOutOfOrder", report);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
        }

        [Fact]
        public void OasisOdf14Corpus_Negative_PageLayoutPropertiesRejectsDuplicateInterleaveBranch()
        {
            string content =
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
                "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
                "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" office:version=\"1.4\">" +
                "<office:automatic-styles><style:page-layout style:name=\"pm1\">" +
                "<style:page-layout-properties>" +
                "<style:columns fo:column-count=\"2\"/>" +
                "<style:columns fo:column-count=\"3\"/>" +
                "</style:page-layout-properties>" +
                "</style:page-layout></office:automatic-styles>" +
                "<office:body><office:text/></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            LogReport("OasisOdf14Corpus_Negative_PageLayoutPropertiesRejectsDuplicateInterleaveBranch", report);
            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF3101" &&
                issue.PackagePath == "content.xml");
        }

        [Fact]
        public void OasisOdf14Corpus_Negative_InvalidContentOrder()
        {
            string flatXml = "<office:document xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.4\" office:mimetype=\"application/vnd.oasis.opendocument.text\"><office:body><office:text/></office:body><office:meta/></office:document>";
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(flatXml));
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);
            LogReport("OasisOdf14Corpus_Negative_InvalidContentOrder", report);
            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF3101" &&
                issue.PackagePath is "content.xml" or "settings.xml");
        }

        [Fact]
        public void OasisOdf14Corpus_Negative_DocumentContentOptionalBlocksAfterBody()
        {
            string content =
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
                "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.4\">" +
                "<office:body><office:text><text:p>Body is too early</text:p></office:text></office:body>" +
                "<office:automatic-styles/>" +
                "</office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            LogReport("OasisOdf14Corpus_Negative_DocumentContentOptionalBlocksAfterBody", report);
            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF3101" &&
                issue.PackagePath == "content.xml");
        }

        [Fact]
        public void OasisOdf14Corpus_Negative_StrictOdfNamespaceExtension()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.4\"><office:body><office:text><text:not-in-schema /></office:text></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(
                package,
                OdfComplianceProfiles.OasisOdf14Strict,
                "document.odt");

            LogReport("OasisOdf14Corpus_Negative_StrictOdfNamespaceExtension", report);
            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "DisallowInvalidOdfNamespaceExtensions" &&
                issue.PackagePath == "content.xml" &&
                issue.XPath == "/office:document-content[1]/office:body[1]/office:text[1]/text:not-in-schema[1]");
        }

        [Fact]
        public void OasisOdf14Corpus_Negative_StrictOdfNamespaceAttributeExtension()
        {
            string content =
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
                "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.4\">" +
                "<office:body><office:text><text:p text:not-in-schema=\"x\">invalid attribute</text:p></office:text></office:body>" +
                "</office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(
                package,
                OdfComplianceProfiles.OasisOdf14Strict,
                "document.odt");

            LogReport("OasisOdf14Corpus_Negative_StrictOdfNamespaceAttributeExtension", report);
            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "DisallowInvalidOdfNamespaceExtensions" &&
                issue.PackagePath == "content.xml" &&
                issue.XPath == "/office:document-content[1]/office:body[1]/office:text[1]/text:p[1]/@not-in-schema");
        }
    }
}
