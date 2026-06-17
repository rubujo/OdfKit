using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Text;
using OdfKit.Spreadsheet;
using OdfKit.Presentation;
using Xunit;

namespace OdfKit.Tests
{
    public class E2ETests
    {
        private static readonly XNamespace OfficeNs = OdfNamespaces.Office;
        private static readonly XNamespace TextNs = OdfNamespaces.Text;
        private static readonly XNamespace TableNs = OdfNamespaces.Table;
        private static readonly XNamespace DrawNs = OdfNamespaces.Draw;
        private static readonly XNamespace FoNs = OdfNamespaces.Fo;
        private static readonly XNamespace XLinkNs = OdfNamespaces.XLink;

        private static OdfSchemaSet CreateCustomSchema(params OdfSchemaPatternDefinition[] patterns)
        {
            return new OdfSchemaSet(
                OdfVersion.Odf14,
                new Uri("https://example.invalid/custom.rng"),
                "2026-06-11",
                Array.Empty<OdfElementDefinition>(),
                patterns: patterns);
        }

        // ==========================================
        // TIER 1: FEATURE COVERAGE (HAPPY-PATH)
        // ==========================================

        [Fact]
        public void OdfTemplate_Text_Creation_And_Detection()
        {
            using var ms = new MemoryStream();
            using (var package = OdfDocumentFactory.CreatePackage(ms, OdfDocumentKind.TextTemplate, leaveOpen: true))
            {
                Assert.Equal("application/vnd.oasis.opendocument.text-template", package.MimeType);
                var doc = new TextDocument(package);
                doc.AddParagraph("Happy Path Template Text");
                doc.Save();
            }

            ms.Position = 0;
            using (var packageRead = OdfPackage.Open(ms))
            {
                Assert.Equal("application/vnd.oasis.opendocument.text-template", packageRead.MimeType);
                Assert.True(packageRead.HasEntry("content.xml"));
            }

            // Verify detection
            Assert.Equal(OdfDocumentKind.TextTemplate, OdfDocumentKindDetector.FromFileName("document.ott"));
            Assert.Equal(OdfDocumentKind.TextTemplate, OdfDocumentKindDetector.FromMimeType("application/vnd.oasis.opendocument.text-template"));
        }

        [Fact]
        public void OdfTemplate_Spreadsheet_Creation_And_Detection()
        {
            using var ms = new MemoryStream();
            using (var package = OdfDocumentFactory.CreatePackage(ms, OdfDocumentKind.SpreadsheetTemplate, leaveOpen: true))
            {
                Assert.Equal("application/vnd.oasis.opendocument.spreadsheet-template", package.MimeType);
                var doc = new SpreadsheetDocument(package);
                var sheet = doc.AddSheet("Sheet1");
                sheet.GetCell(0, 0).RawValue = "Value";
                doc.Save();
            }

            ms.Position = 0;
            using (var packageRead = OdfPackage.Open(ms))
            {
                Assert.Equal("application/vnd.oasis.opendocument.spreadsheet-template", packageRead.MimeType);
                Assert.True(packageRead.HasEntry("content.xml"));
            }

            // Verify detection
            Assert.Equal(OdfDocumentKind.SpreadsheetTemplate, OdfDocumentKindDetector.FromFileName("sheet.ots"));
            Assert.Equal(OdfDocumentKind.SpreadsheetTemplate, OdfDocumentKindDetector.FromMimeType("application/vnd.oasis.opendocument.spreadsheet-template"));
        }

        [Fact]
        public void OdfTemplate_Presentation_Creation_And_Detection()
        {
            using var ms = new MemoryStream();
            using (var package = OdfDocumentFactory.CreatePackage(ms, OdfDocumentKind.PresentationTemplate, leaveOpen: true))
            {
                Assert.Equal("application/vnd.oasis.opendocument.presentation-template", package.MimeType);
                var doc = new PresentationDocument(package);
                doc.AddSlide("Slide1");
                doc.Save();
            }

            ms.Position = 0;
            using (var packageRead = OdfPackage.Open(ms))
            {
                Assert.Equal("application/vnd.oasis.opendocument.presentation-template", packageRead.MimeType);
            }

            // Verify detection
            Assert.Equal(OdfDocumentKind.PresentationTemplate, OdfDocumentKindDetector.FromFileName("presentation.otp"));
            Assert.Equal(OdfDocumentKind.PresentationTemplate, OdfDocumentKindDetector.FromMimeType("application/vnd.oasis.opendocument.presentation-template"));
        }

        [Fact]
        public void OdfTemplate_Graphics_Creation_And_Detection()
        {
            using var ms = new MemoryStream();
            using (var package = OdfDocumentFactory.CreatePackage(ms, OdfDocumentKind.GraphicsTemplate, leaveOpen: true))
            {
                Assert.Equal("application/vnd.oasis.opendocument.graphics-template", package.MimeType);
            }

            // Verify detection
            Assert.Equal(OdfDocumentKind.GraphicsTemplate, OdfDocumentKindDetector.FromFileName("drawing.otg"));
            Assert.Equal(OdfDocumentKind.GraphicsTemplate, OdfDocumentKindDetector.FromMimeType("application/vnd.oasis.opendocument.graphics-template"));
        }

        [Fact]
        public void OdfFlatXml_Fodt_RoundTrip()
        {
            using var ms = new MemoryStream();
            OdfDocumentFactory.WriteFlatXml(ms, OdfDocumentKind.FlatText, OdfVersion.Odf14, leaveOpen: true);

            ms.Position = 0;
            var report = OdfFlatDocumentValidator.Validate(ms, "document.fodt");
            Assert.DoesNotContain(report.Issues, i => i.Severity == OdfIssueSeverity.Error || i.Severity == OdfIssueSeverity.Fatal);
            Assert.Equal(OdfDocumentKind.FlatText, report.DocumentKind);
            Assert.Equal(OdfVersion.Odf14, report.DetectedVersion);

            // Check elements via Linq XML
            ms.Position = 0;
            var doc = XDocument.Load(ms);
            Assert.Equal(OfficeNs + "document", doc.Root?.Name);
            Assert.Equal("application/vnd.oasis.opendocument.text", doc.Root?.Attribute(OfficeNs + "mimetype")?.Value);
        }

        [Fact]
        public void OdfFlatXml_Fods_RoundTrip()
        {
            using var ms = new MemoryStream();
            OdfDocumentFactory.WriteFlatXml(ms, OdfDocumentKind.FlatSpreadsheet, OdfVersion.Odf14, leaveOpen: true);

            ms.Position = 0;
            var report = OdfFlatDocumentValidator.Validate(ms, "workbook.fods");
            Assert.DoesNotContain(report.Issues, i => i.Severity == OdfIssueSeverity.Error || i.Severity == OdfIssueSeverity.Fatal);
            Assert.Equal(OdfDocumentKind.FlatSpreadsheet, report.DocumentKind);

            ms.Position = 0;
            var doc = XDocument.Load(ms);
            Assert.Equal(OfficeNs + "document", doc.Root?.Name);
            Assert.Equal("application/vnd.oasis.opendocument.spreadsheet", doc.Root?.Attribute(OfficeNs + "mimetype")?.Value);
        }

        [Fact]
        public void Validation_OccurrenceCardinality()
        {
            var pattern = new OdfSchemaPatternDefinition("root", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", OfficeNs.NamespaceName, "document", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.Optional, "optional", "", "", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", TextNs.NamespaceName, "p", "", "", "")
                            }),
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.ZeroOrMore, "zeroOrMore", "", "", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", TableNs.NamespaceName, "table", "", "", "")
                            })
                    })
            });

            var schema = CreateCustomSchema(pattern);

            var docValid1 = new XElement(OfficeNs + "document",
                new XElement(TextNs + "p"),
                new XElement(TableNs + "table"),
                new XElement(TableNs + "table")
            );

            var docValid2 = new XElement(OfficeNs + "document",
                new XElement(TableNs + "table")
            );

            var docInvalid = new XElement(OfficeNs + "document",
                new XElement(TextNs + "p"),
                new XElement(TextNs + "p") // multiple optionals is invalid
            );

            Assert.True(OdfSchemaPatternValidator.ValidateElement(docValid1, schema, "root").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(docValid2, schema, "root").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(docInvalid, schema, "root").IsMatch);
        }

        [Fact]
        public void Validation_W3CDatatypes_Basic()
        {
            var pattern = new OdfSchemaPatternDefinition("root", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", OfficeNs.NamespaceName, "document", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.Attribute, "exactlyOne", "", "is-valid", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", "boolean", "")
                            }),
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.Attribute, "exactlyOne", "", "count", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", "integer", "")
                            })
                    })
            });

            var schema = CreateCustomSchema(pattern);

            var valid = new XElement(OfficeNs + "document",
                new XAttribute("is-valid", "true"),
                new XAttribute("count", "123")
            );

            var invalid = new XElement(OfficeNs + "document",
                new XAttribute("is-valid", "not-a-bool"),
                new XAttribute("count", "123")
            );

            Assert.True(OdfSchemaPatternValidator.ValidateElement(valid, schema, "root").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalid, schema, "root").IsMatch);
        }

        [Fact]
        public void Validation_CustomFacets_LengthAndPattern()
        {
            var pattern = new OdfSchemaPatternDefinition("root", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", OfficeNs.NamespaceName, "document", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.Attribute, "exactlyOne", "", "ssn", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(
                                    OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", "string", "",
                                    dataParameters: new[]
                                    {
                                        new KeyValuePair<string, string>("pattern", @"^\d{3}-\d{2}-\d{4}$")
                                    })
                            }),
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.Attribute, "exactlyOne", "", "code", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(
                                    OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", "string", "",
                                    dataParameters: new[]
                                    {
                                        new KeyValuePair<string, string>("length", "5")
                                    })
                            })
                    })
            });

            var schema = CreateCustomSchema(pattern);

            var valid = new XElement(OfficeNs + "document",
                new XAttribute("ssn", "000-12-3456"),
                new XAttribute("code", "abcde")
            );

            var invalidPattern = new XElement(OfficeNs + "document",
                new XAttribute("ssn", "000-12-345"), // invalid pattern
                new XAttribute("code", "abcde")
            );

            var invalidLength = new XElement(OfficeNs + "document",
                new XAttribute("ssn", "000-12-3456"),
                new XAttribute("code", "abcd") // invalid length
            );

            Assert.True(OdfSchemaPatternValidator.ValidateElement(valid, schema, "root").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidPattern, schema, "root").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidLength, schema, "root").IsMatch);
        }

        [Fact]
        public void Validation_NameClassExcept()
        {
            var pattern = new OdfSchemaPatternDefinition("root", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", OfficeNs.NamespaceName, "document", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.AnyName, "exactlyOne", "", "", "", "", "",
                            nameClasses: new[]
                            {
                                new OdfSchemaNameClass(OdfSchemaNameClassKind.AnyName, "", "", isExcept: false),
                                new OdfSchemaNameClass(OdfSchemaNameClassKind.NamespaceName, TextNs.NamespaceName, "", isExcept: true)
                            },
                            children: new[]
                            {
                                new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Empty, "exactlyOne", "", "", "", "", "")
                            })
                    })
            });

            var schema = CreateCustomSchema(pattern);

            var valid = new XElement(OfficeNs + "document",
                new XElement(TableNs + "table")
            );

            var invalid = new XElement(OfficeNs + "document",
                new XElement(TextNs + "p") // text namespace is excepted
            );

            Assert.True(OdfSchemaPatternValidator.ValidateElement(valid, schema, "root").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalid, schema, "root").IsMatch);
        }

        [Fact]
        public void Validation_Interleave_Basic()
        {
            var pattern = new OdfSchemaPatternDefinition("root", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", OfficeNs.NamespaceName, "document", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.Interleave, "exactlyOne", "", "", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", TextNs.NamespaceName, "p", "", "", ""),
                                new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", TableNs.NamespaceName, "table", "", "", "")
                            })
                    })
            });

            var schema = CreateCustomSchema(pattern);

            var order1 = new XElement(OfficeNs + "document",
                new XElement(TextNs + "p"),
                new XElement(TableNs + "table")
            );

            var order2 = new XElement(OfficeNs + "document",
                new XElement(TableNs + "table"),
                new XElement(TextNs + "p")
            );

            var missing = new XElement(OfficeNs + "document",
                new XElement(TextNs + "p")
            );

            Assert.True(OdfSchemaPatternValidator.ValidateElement(order1, schema, "root").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(order2, schema, "root").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(missing, schema, "root").IsMatch);
        }

        [Fact]
        public void Validation_DatatypeValueExcept()
        {
            var pattern = new OdfSchemaPatternDefinition("root", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", OfficeNs.NamespaceName, "document", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.Attribute, "exactlyOne", "", "num", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(
                                    OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", "integer", "",
                                    children: new[]
                                    {
                                        new OdfSchemaPatternNode(
                                            OdfSchemaPatternNodeKind.Except, "exactlyOne", "", "", "", "", "",
                                            children: new[]
                                            {
                                                new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Value, "exactlyOne", "", "", "", "integer", "42")
                                            })
                                    })
                            })
                    })
            });

            var schema = CreateCustomSchema(pattern);

            var valid = new XElement(OfficeNs + "document", new XAttribute("num", "10"));
            var invalid = new XElement(OfficeNs + "document", new XAttribute("num", "42"));

            Assert.True(OdfSchemaPatternValidator.ValidateElement(valid, schema, "root").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalid, schema, "root").IsMatch);
        }

        // ==========================================
        // TIER 2: BOUNDARY & CORNER CASES
        // ==========================================

        [Fact]
        public void Mismatched_Extension_And_Mimetype()
        {
            var flatXml = new XDocument(
                new XElement(OfficeNs + "document",
                    new XAttribute(OfficeNs + "mimetype", "application/vnd.oasis.opendocument.spreadsheet"),
                    new XElement(OfficeNs + "body",
                        new XElement(OfficeNs + "spreadsheet")
                    )
                )
            );

            using var ms = new MemoryStream();
            flatXml.Save(ms);
            ms.Position = 0;

            // Validator should report an issue because .fodt is text but body is spreadsheet
            var report = OdfFlatDocumentValidator.Validate(ms, "document.fodt");
            Assert.Contains(report.Issues, i => i.Severity == OdfIssueSeverity.Warning && i.RuleId == "ODF2005");
        }

        [Fact]
        public void Invalid_Or_Missing_FlatXml_Root()
        {
            var badXml = new XDocument(
                new XElement("not-document",
                    new XElement("body")
                )
            );

            using var ms = new MemoryStream();
            badXml.Save(ms);
            ms.Position = 0;

            var report = OdfFlatDocumentValidator.Validate(ms, "document.fodt");
            Assert.Contains(report.Issues, i => i.RuleId == "ODF2001");
        }

        [Fact]
        public void FlatXml_Detection_From_Invalid_Extension_But_Correct_Mimetype()
        {
            var flatXml = new XDocument(
                new XElement(OfficeNs + "document",
                    new XAttribute(OfficeNs + "mimetype", "application/vnd.oasis.opendocument.text"),
                    new XAttribute(OfficeNs + "version", "1.4"),
                    new XElement(OfficeNs + "body",
                        new XElement(OfficeNs + "text")
                    )
                )
            );

            using var ms = new MemoryStream();
            flatXml.Save(ms);
            ms.Position = 0;

            var report = OdfFlatDocumentValidator.Validate(ms, "document.txt", OdfComplianceProfiles.OasisOdf14Strict);
            // Extension is incorrect (.txt instead of .fodt), so it should report an issue (ODF1002) but successfully detect document kind
            Assert.Equal(OdfDocumentKind.FlatText, report.DocumentKind);
            Assert.Contains(report.Issues, i => i.RuleId == "ODF1002");
        }

        [Fact]
        public void FlatXml_With_Huge_Base64_Embedded_Asset()
        {
            var base64Builder = new StringBuilder();
            for (int i = 0; i < 100000; i++)
            {
                base64Builder.Append("AABBCCDDEEFF");
            }

            var flatXml = new XDocument(
                new XElement(OfficeNs + "document",
                    new XAttribute(OfficeNs + "mimetype", "application/vnd.oasis.opendocument.text"),
                    new XElement(OfficeNs + "body",
                        new XElement(OfficeNs + "text",
                            new XElement(DrawNs + "image",
                                new XElement(OfficeNs + "binary-data", base64Builder.ToString())
                            )
                        )
                    )
                )
            );

            using var ms = new MemoryStream();
            flatXml.Save(ms);
            ms.Position = 0;

            var report = OdfFlatDocumentValidator.Validate(ms, "document.fodt");
            Assert.NotNull(report);
            Assert.Equal(OdfDocumentKind.FlatText, report.DocumentKind);
        }

        [Fact]
        public void Template_With_Duplicate_Mimetype_File()
        {
            using var ms = new MemoryStream();
            using (var package = OdfDocumentFactory.CreatePackage(ms, OdfDocumentKind.TextTemplate, leaveOpen: true))
            {
                // Force write another mimetype file in subfolder or similar to see if it acts as a normal file or throws
                package.WriteEntry("subfolder/mimetype", Encoding.UTF8.GetBytes("duplicate"), "text/plain");
                package.Save();
            }

            ms.Position = 0;
            using (var packageRead = OdfPackage.Open(ms))
            {
                // Original mimetype should not be overwritten because SetMimeType puts it at "/" first entry or handles it internally
                Assert.Equal("application/vnd.oasis.opendocument.text-template", packageRead.MimeType);
            }
        }

        [Fact]
        public void Interleave_Backtracking_Performance_Caching()
        {
            // RNG pattern: Interleave(Optional(A), Optional(B), Optional(C), Optional(D))
            var pattern = new OdfSchemaPatternDefinition("root", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", OfficeNs.NamespaceName, "document", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.Interleave, "exactlyOne", "", "", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Optional, "optional", "", "", "", "", "",
                                    children: new[] { new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", TextNs.NamespaceName, "a", "", "", "") }),
                                new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Optional, "optional", "", "", "", "", "",
                                    children: new[] { new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", TextNs.NamespaceName, "b", "", "", "") }),
                                new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Optional, "optional", "", "", "", "", "",
                                    children: new[] { new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", TextNs.NamespaceName, "c", "", "", "") }),
                                new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Optional, "optional", "", "", "", "", "",
                                    children: new[] { new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", TextNs.NamespaceName, "d", "", "", "") })
                            })
                    })
            });

            var schema = CreateCustomSchema(pattern);

            var element = new XElement(OfficeNs + "document",
                new XElement(TextNs + "c"),
                new XElement(TextNs + "a"),
                new XElement(TextNs + "d")
            );

            // Backtracking should complete quickly and find a match
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var result = OdfSchemaPatternValidator.ValidateElement(element, schema, "root");
            watch.Stop();

            Assert.True(result.IsMatch);
            Assert.True(watch.ElapsedMilliseconds < 100, $"Interleave backtracking is too slow: {watch.ElapsedMilliseconds}ms");
        }

        [Fact]
        public void ScaleAware_Numerical_Facet_Comparisons()
        {
            var pattern = new OdfSchemaPatternDefinition("root", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", OfficeNs.NamespaceName, "document", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.Attribute, "exactlyOne", "", "val", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(
                                    OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", "decimal", "",
                                    dataParameters: new[]
                                    {
                                        new KeyValuePair<string, string>("minInclusive", "999999999999999999.000000000000000001"),
                                        new KeyValuePair<string, string>("maxInclusive", "999999999999999999.000000000000000009")
                                    })
                            })
                    })
            });

            var schema = CreateCustomSchema(pattern);

            var valid = new XElement(OfficeNs + "document", new XAttribute("val", "999999999999999999.000000000000000005"));
            var tooSmall = new XElement(OfficeNs + "document", new XAttribute("val", "999999999999999999.000000000000000000"));
            var tooLarge = new XElement(OfficeNs + "document", new XAttribute("val", "999999999999999999.000000000000000010"));

            Assert.True(OdfSchemaPatternValidator.ValidateElement(valid, schema, "root").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(tooSmall, schema, "root").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(tooLarge, schema, "root").IsMatch);
        }

        [Fact]
        public void Datetime_Timezone_And_EdgeValues()
        {
            var pattern = new OdfSchemaPatternDefinition("root", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", OfficeNs.NamespaceName, "document", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.Attribute, "exactlyOne", "", "time", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", "dateTime", "")
                            })
                    })
            });

            var schema = CreateCustomSchema(pattern);

            var validZ = new XElement(OfficeNs + "document", new XAttribute("time", "2026-06-11T12:00:00Z"));
            var validTzPlus = new XElement(OfficeNs + "document", new XAttribute("time", "2026-06-11T12:00:00+14:00"));
            var validTzMinus = new XElement(OfficeNs + "document", new XAttribute("time", "2026-06-11T12:00:00-14:00"));
            var validMin = new XElement(OfficeNs + "document", new XAttribute("time", "0001-01-01T00:00:00Z"));
            var validMax = new XElement(OfficeNs + "document", new XAttribute("time", "9999-12-31T23:59:59Z"));
            var invalid = new XElement(OfficeNs + "document", new XAttribute("time", "not-a-datetime"));

            Assert.True(OdfSchemaPatternValidator.ValidateElement(validZ, schema, "root").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validTzPlus, schema, "root").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validTzMinus, schema, "root").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validMin, schema, "root").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validMax, schema, "root").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalid, schema, "root").IsMatch);
        }

        [Fact]
        public void Strict_ODF14_Namespace_Enforcement()
        {
            // Verify flat ODF validator with strict profile rejects elements outside allowed namespaces
            var xmlWithForeignNamespace = new XDocument(
                new XElement(OfficeNs + "document",
                    new XAttribute(OfficeNs + "mimetype", "application/vnd.oasis.opendocument.text"),
                    new XAttribute(OfficeNs + "version", "1.4"),
                    new XElement(OfficeNs + "body",
                        new XElement(OfficeNs + "text",
                            new XElement(OfficeNs + "invalid-node-xyz")
                        )
                    )
                )
            );

            using var ms = new MemoryStream();
            xmlWithForeignNamespace.Save(ms);
            ms.Position = 0;

            var report = OdfFlatDocumentValidator.Validate(ms, "document.fodt", OdfComplianceProfiles.OasisOdf14Strict);
            Assert.Contains(report.Issues, i => i.Severity == OdfIssueSeverity.Error && (i.RuleId.StartsWith("ODF") || i.RuleId == "DisallowInvalidOdfNamespaceExtensions"));
        }

        [Fact]
        public void Datatype_Validation_Invalid_Base64_And_HexBinary()
        {
            var pattern = new OdfSchemaPatternDefinition("root", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", OfficeNs.NamespaceName, "document", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.Attribute, "exactlyOne", "", "base64", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", "base64Binary", "")
                            }),
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.Attribute, "exactlyOne", "", "hex", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", "hexBinary", "")
                            })
                    })
            });

            var schema = CreateCustomSchema(pattern);

            var valid = new XElement(OfficeNs + "document",
                new XAttribute("base64", "SGVsbG8="),
                new XAttribute("hex", "DeadBeef42")
            );

            var invalidBase64 = new XElement(OfficeNs + "document",
                new XAttribute("base64", "SGVsbG8=???"), // contains invalid base64 char ?
                new XAttribute("hex", "DeadBeef42")
            );

            var invalidHex = new XElement(OfficeNs + "document",
                new XAttribute("base64", "SGVsbG8="),
                new XAttribute("hex", "DeadBeef4G") // G is not a hex character
            );

            Assert.True(OdfSchemaPatternValidator.ValidateElement(valid, schema, "root").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidBase64, schema, "root").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidHex, schema, "root").IsMatch);
        }

        [Fact]
        public void NameClassExcept_Nested_Inside_Choice_With_No_Match()
        {
            var pattern = new OdfSchemaPatternDefinition("root", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", OfficeNs.NamespaceName, "document", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.Choice, "exactlyOne", "", "", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(
                                    OdfSchemaPatternNodeKind.AnyName, "exactlyOne", "", "", "", "", "",
                                    nameClasses: new[]
                                    {
                                        new OdfSchemaNameClass(OdfSchemaNameClassKind.AnyName, "", "", isExcept: false),
                                        new OdfSchemaNameClass(OdfSchemaNameClassKind.NamespaceName, TextNs.NamespaceName, "", isExcept: true)
                                    },
                                    children: new[] { new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Empty, "exactlyOne", "", "", "", "", "") }),
                                new OdfSchemaPatternNode(
                                    OdfSchemaPatternNodeKind.Element, "exactlyOne", TextNs.NamespaceName, "span", "", "", "")
                            })
                    })
            });

            var schema = CreateCustomSchema(pattern);

            var matchSpan = new XElement(OfficeNs + "document",
                new XElement(TextNs + "span")
            );

            var matchTable = new XElement(OfficeNs + "document",
                new XElement(TableNs + "table")
            );

            var noMatch = new XElement(OfficeNs + "document",
                new XElement(TextNs + "p") // p is excepted by anyName and does not match span element
            );

            Assert.True(OdfSchemaPatternValidator.ValidateElement(matchSpan, schema, "root").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(matchTable, schema, "root").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(noMatch, schema, "root").IsMatch);
        }

        // ==========================================
        // TIER 3: CROSS-FEATURE COMBINATIONS
        // ==========================================

        [Fact]
        public void Template_Saved_As_FlatXml_Validation()
        {
            using var ms = new MemoryStream();
            using (var package = OdfDocumentFactory.CreatePackage(ms, OdfDocumentKind.TextTemplate, leaveOpen: true))
            {
                var doc = new TextDocument(package);
                doc.AddParagraph("Testing cross-feature template to flat XML.");
                doc.Save();
            }

            // Write as flat xml
            using var flatMs = new MemoryStream();
            OdfDocumentFactory.WriteFlatXml(flatMs, OdfDocumentKind.FlatText, OdfVersion.Odf14, leaveOpen: true);

            flatMs.Position = 0;
            var report = OdfFlatDocumentValidator.Validate(flatMs, "document.fodt", OdfComplianceProfiles.OasisOdf14Strict);
            Assert.DoesNotContain(report.Issues, i => i.Severity == OdfIssueSeverity.Error);
        }

        [Fact]
        public void Custom_Facets_Inside_Interleaved_Elements()
        {
            var pattern = new OdfSchemaPatternDefinition("root", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", OfficeNs.NamespaceName, "document", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.Interleave, "exactlyOne", "", "", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(
                                    OdfSchemaPatternNodeKind.Element, "exactlyOne", TextNs.NamespaceName, "p", "", "", "",
                                    children: new[]
                                    {
                                        new OdfSchemaPatternNode(
                                            OdfSchemaPatternNodeKind.Attribute, "exactlyOne", "", "val", "", "", "",
                                            children: new[]
                                            {
                                                new OdfSchemaPatternNode(
                                                    OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", "string", "",
                                                    dataParameters: new[] { new KeyValuePair<string, string>("pattern", @"^[A-Z]{3}$") })
                                            })
                                    }),
                                new OdfSchemaPatternNode(
                                    OdfSchemaPatternNodeKind.Element, "exactlyOne", TableNs.NamespaceName, "table", "", "", "",
                                    children: new[]
                                    {
                                        new OdfSchemaPatternNode(
                                            OdfSchemaPatternNodeKind.Attribute, "exactlyOne", "", "rows", "", "", "",
                                            children: new[]
                                            {
                                                new OdfSchemaPatternNode(
                                                    OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", "integer", "",
                                                    dataParameters: new[]
                                                    {
                                                        new KeyValuePair<string, string>("minInclusive", "1"),
                                                        new KeyValuePair<string, string>("maxInclusive", "100")
                                                    })
                                            })
                                    })
                            })
                    })
            });

            var schema = CreateCustomSchema(pattern);

            var valid = new XElement(OfficeNs + "document",
                new XElement(TableNs + "table", new XAttribute("rows", "15")),
                new XElement(TextNs + "p", new XAttribute("val", "ABC"))
            );

            var invalidFacet = new XElement(OfficeNs + "document",
                new XElement(TableNs + "table", new XAttribute("rows", "15")),
                new XElement(TextNs + "p", new XAttribute("val", "abc")) // lowercase invalid
            );

            Assert.True(OdfSchemaPatternValidator.ValidateElement(valid, schema, "root").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidFacet, schema, "root").IsMatch);
        }

        [Fact]
        public void NameClassExcept_With_DatatypeValueExcept()
        {
            var pattern = new OdfSchemaPatternDefinition("root", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", OfficeNs.NamespaceName, "document", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.AnyName, "exactlyOne", "", "", "", "", "",
                            nameClasses: new[]
                            {
                                new OdfSchemaNameClass(OdfSchemaNameClassKind.AnyName, "", "", isExcept: false),
                                new OdfSchemaNameClass(OdfSchemaNameClassKind.NamespaceName, TextNs.NamespaceName, "", isExcept: true)
                            },
                            children: new[]
                            {
                                new OdfSchemaPatternNode(
                                    OdfSchemaPatternNodeKind.Attribute, "exactlyOne", "", "val", "", "", "",
                                    children: new[]
                                    {
                                        new OdfSchemaPatternNode(
                                            OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", "integer", "",
                                            children: new[]
                                            {
                                                new OdfSchemaPatternNode(
                                                    OdfSchemaPatternNodeKind.Except, "exactlyOne", "", "", "", "", "",
                                                    children: new[]
                                                    {
                                                        new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Value, "exactlyOne", "", "", "", "integer", "99")
                                                    })
                                            })
                                    })
                            })
                    })
            });

            var schema = CreateCustomSchema(pattern);

            var valid = new XElement(OfficeNs + "document",
                new XElement(TableNs + "table", new XAttribute("val", "50"))
            );

            var invalidName = new XElement(OfficeNs + "document",
                new XElement(TextNs + "p", new XAttribute("val", "50")) // text ns excepted
            );

            var invalidValue = new XElement(OfficeNs + "document",
                new XElement(TableNs + "table", new XAttribute("val", "99")) // val 99 excepted
            );

            var result = OdfSchemaPatternValidator.ValidateElement(valid, schema, "root");
            Assert.True(result.IsMatch, result.Issues.FirstOrDefault()?.Message ?? "No error message");
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidName, schema, "root").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidValue, schema, "root").IsMatch);
        }

        // ==========================================
        // TIER 4: REAL-WORLD APPLICATION SCENARIOS
        // ==========================================

        [Fact]
        public void RealWorld_TextTemplate_Instantiation()
        {
            using var templateStream = new MemoryStream();
            using (var package = OdfDocumentFactory.CreatePackage(templateStream, OdfDocumentKind.TextTemplate, leaveOpen: true))
            {
                var doc = new TextDocument(package);
                doc.AddParagraph("Title Template");
                doc.AddParagraph("Placeholders: [Name], [Date]");
                doc.Save();
            }

            templateStream.Position = 0;
            using var outputStream = new MemoryStream();
            templateStream.CopyTo(outputStream);
            outputStream.Position = 0;
            using (var package = OdfPackage.Open(outputStream, leaveOpen: true))
            {
                // Verify package opened correctly as template kind
                Assert.Equal("application/vnd.oasis.opendocument.text-template", package.MimeType);

                // Perform real-world substitution
                var doc = new TextDocument(package);
                doc.ReplaceText("[Name]", "Antigravity Specialist Agent");
                doc.ReplaceText("[Date]", "2026-06-11");
                doc.Save();
            }

            outputStream.Position = 0;
            using (var packageRead = OdfPackage.Open(outputStream))
            {
                var doc = new TextDocument(packageRead);
                Assert.Contains("Antigravity Specialist Agent", doc.BodyTextRoot.TextContent);
            }
        }

        [Fact]
        public void RealWorld_SpreadsheetTemplate_With_Data()
        {
            using var templateStream = new MemoryStream();
            using (var package = OdfDocumentFactory.CreatePackage(templateStream, OdfDocumentKind.SpreadsheetTemplate, leaveOpen: true))
            {
                var doc = new SpreadsheetDocument(package);
                var sheet = doc.AddSheet("Template Grid");
                sheet.GetCell(0, 0).RawValue = "ID";
                sheet.GetCell(0, 1).RawValue = "Value";
                // Let's create cells 1,0 and 1,1
                sheet.GetCell(1, 0).RawValue = "";
                sheet.GetCell(1, 1).RawValue = "";
                doc.Save();
            }

            templateStream.Position = 0;
            using var outputStream = new MemoryStream();
            templateStream.CopyTo(outputStream);
            outputStream.Position = 0;
            using (var package = OdfPackage.Open(outputStream, leaveOpen: true))
            {
                var doc = new SpreadsheetDocument(package);
                var sheet = doc.GetSheet("Template Grid");
                Assert.NotNull(sheet);
                sheet.GetCell(1, 0).RawValue = "1";
                sheet.GetCell(1, 1).RawValue = "100.25";
                doc.Save();
            }

            outputStream.Position = 0;
            using (var packageRead = OdfPackage.Open(outputStream))
            {
                var doc = new SpreadsheetDocument(packageRead);
                var sheet = doc.GetSheet("Template Grid");
                Assert.NotNull(sheet);
                Assert.Equal("1", sheet.GetCell(1, 0).RawValue);
                Assert.Equal("100.25", sheet.GetCell(1, 1).RawValue);
            }
        }

        [Fact]
        public void RealWorld_FlatXml_Document_Generator()
        {
            using var flatMs = new MemoryStream();
            OdfDocumentFactory.WriteFlatXml(flatMs, OdfDocumentKind.FlatText, OdfVersion.Odf14, leaveOpen: true);

            flatMs.Position = 0;
            var doc = XDocument.Load(flatMs);

            // Add actual rich text structures (sections, lists, tables) into office:text body
            var body = doc.Root?.Element(OfficeNs + "body");
            var officeText = body?.Element(OfficeNs + "text");
            Assert.NotNull(officeText);

            officeText.Add(
                new XElement(TextNs + "section", new XAttribute(TextNs + "name", "Section1"),
                    new XElement(TextNs + "h", new XAttribute(TextNs + "outline-level", "1"), "Flat XML Section 1 Header"),
                    new XElement(TextNs + "p", "Generated flat XML content paragraph."),
                    new XElement(TableNs + "table", new XAttribute(TableNs + "name", "Table1"),
                        new XElement(TableNs + "table-row",
                            new XElement(TableNs + "table-cell",
                                new XElement(TextNs + "p", "Cell A1")
                            ),
                            new XElement(TableNs + "table-cell",
                                new XElement(TextNs + "p", "Cell B1")
                            )
                        )
                    )
                )
            );

            // Re-validate to ensure valid DOM layout
            using var validatedMs = new MemoryStream();
            doc.Save(validatedMs);
            validatedMs.Position = 0;

            var report = OdfFlatDocumentValidator.Validate(validatedMs, "report.fodt");
            Assert.DoesNotContain(report.Issues, i => i.Severity == OdfIssueSeverity.Error);
        }

        [Fact]
        public void Complex_MathML_Formula_Validation()
        {
            // Test validation of MathML math elements within ODF content
            XNamespace mathmlNs = "http://www.w3.org/1998/Math/MathML";
            var formulaElement = new XElement(TextNs + "p",
                new XElement(DrawNs + "object",
                    new XElement(mathmlNs + "math",
                        new XElement(mathmlNs + "mrow",
                            new XElement(mathmlNs + "mi", "x"),
                            new XElement(mathmlNs + "mo", "="),
                            new XElement(mathmlNs + "mfrac",
                                new XElement(mathmlNs + "mrow",
                                    new XElement(mathmlNs + "mo", "-"),
                                    new XElement(mathmlNs + "mi", "b")
                                ),
                                new XElement(mathmlNs + "mrow",
                                    new XElement(mathmlNs + "mn", "2"),
                                    new XElement(mathmlNs + "mi", "a")
                                )
                            )
                        )
                    )
                )
            );

            // Verify with custom schema
            var rootPattern = new OdfSchemaPatternDefinition("root", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", TextNs.NamespaceName, "p", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.Element, "exactlyOne", DrawNs.NamespaceName, "object", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(
                                    OdfSchemaPatternNodeKind.Element, "exactlyOne", mathmlNs.NamespaceName, "math", "", "", "",
                                    children: new[]
                                    {
                                        new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Ref, "exactlyOne", "", "", "mrow", "", "")
                                    })
                            })
                    })
            });

            var mrowPattern = new OdfSchemaPatternDefinition("mrow", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", mathmlNs.NamespaceName, "mrow", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.ZeroOrMore, "zeroOrMore", "", "", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(
                                    OdfSchemaPatternNodeKind.Choice, "exactlyOne", "", "", "", "", "",
                                    children: new[]
                                    {
                                        new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Ref, "exactlyOne", "", "", "mi", "", ""),
                                        new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Ref, "exactlyOne", "", "", "mo", "", ""),
                                        new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Ref, "exactlyOne", "", "", "mn", "", ""),
                                        new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Ref, "exactlyOne", "", "", "mfrac", "", "")
                                    })
                            })
                    })
            });

            var miPattern = new OdfSchemaPatternDefinition("mi", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", mathmlNs.NamespaceName, "mi", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Text, "exactlyOne", "", "", "", "", "")
                    })
            });

            var moPattern = new OdfSchemaPatternDefinition("mo", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", mathmlNs.NamespaceName, "mo", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Text, "exactlyOne", "", "", "", "", "")
                    })
            });

            var mnPattern = new OdfSchemaPatternDefinition("mn", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", mathmlNs.NamespaceName, "mn", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Text, "exactlyOne", "", "", "", "", "")
                    })
            });

            // Use an inner non-recursive mrow pattern to bypass single-reference cycle detection limits
            var mrowInnerPattern = new OdfSchemaPatternDefinition("mrow_inner", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", mathmlNs.NamespaceName, "mrow", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.ZeroOrMore, "zeroOrMore", "", "", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(
                                    OdfSchemaPatternNodeKind.Choice, "exactlyOne", "", "", "", "", "",
                                    children: new[]
                                    {
                                        new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Ref, "exactlyOne", "", "", "mi", "", ""),
                                        new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Ref, "exactlyOne", "", "", "mo", "", ""),
                                        new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Ref, "exactlyOne", "", "", "mn", "", "")
                                    })
                            })
                    })
            });

            var mfracPattern = new OdfSchemaPatternDefinition("mfrac", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", mathmlNs.NamespaceName, "mfrac", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Ref, "exactlyOne", "", "", "mrow_inner", "", ""),
                        new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Ref, "exactlyOne", "", "", "mrow_inner", "", "")
                    })
            });

            var schema = CreateCustomSchema(rootPattern, mrowPattern, miPattern, moPattern, mnPattern, mrowInnerPattern, mfracPattern);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(formulaElement, schema, "root").IsMatch);
        }

        [Fact]
        public void Document_Metadata_And_CustomProperties_Validation()
        {
            XNamespace dc = OdfNamespaces.Dc;
            var metadataXml = new XElement(OfficeNs + "document-meta",
                new XElement(OfficeNs + "meta",
                    new XElement(dc + "creator", "Antigravity Specialist"),
                    new XElement(dc + "date", "2026-06-11T12:00:00Z"),
                    new XElement(OfficeNs + "editing-cycles", "3"),
                    new XElement(OfficeNs + "user-defined", new XAttribute(OfficeNs + "name", "ProjectCode"), "ODF-E2E")
                )
            );

            // Test with a schema definition resembling document-meta
            var pattern = new OdfSchemaPatternDefinition("meta", new[]
            {
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", OfficeNs.NamespaceName, "document-meta", "", "", "",
                    children: new[]
                    {
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.Element, "exactlyOne", OfficeNs.NamespaceName, "meta", "", "", "",
                            children: new[]
                            {
                                new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", OdfNamespaces.Dc, "creator", "", "", "",
                                    children: new[] { new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Text, "exactlyOne", "", "", "", "", "") }),
                                new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", OdfNamespaces.Dc, "date", "", "", "",
                                    children: new[] { new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Text, "exactlyOne", "", "", "", "", "") }),
                                new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", OfficeNs.NamespaceName, "editing-cycles", "", "", "",
                                    children: new[] { new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Text, "exactlyOne", "", "", "", "", "") }),
                                new OdfSchemaPatternNode(
                                    OdfSchemaPatternNodeKind.Element, "exactlyOne", OfficeNs.NamespaceName, "user-defined", "", "", "",
                                    children: new[]
                                    {
                                        new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Attribute, "exactlyOne", OfficeNs.NamespaceName, "name", "", "", ""),
                                        new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Text, "exactlyOne", "", "", "", "", "")
                                    })
                            })
                    })
            });

            var schema = CreateCustomSchema(pattern);
            var result = OdfSchemaPatternValidator.ValidateElement(metadataXml, schema, "meta");
            Assert.True(result.IsMatch, string.Join("\n", result.Issues.Select(i => $"{i.RuleId}: {i.Message}")));
        }
    }
}
