using System.Globalization;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Database;
using OdfKit.Drawing;
using OdfKit.Image;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests
{
    [Trait(TestCategories.Kind, TestCategories.Compliance)]
    public partial class ComplianceTests
    {
        [Fact]
        public void BuiltInProfilesIncludeRequiredFirstWaveProfiles()
        {
            string[] ids =
            {
                "OASIS_ODF_1_4_Strict",
                "OASIS_ODF_1_4_Extended",
                "ISO_IEC_26300_2015",
                "EU_InteroperableEurope",
                "EU_OfficeDocumentExchange",
                "ROC_Taiwan_ODF_CNS15251",
                "ROC_Taiwan_GovernmentODFTools"
            };

            foreach (string id in ids)
            {
                Assert.NotNull(OdfComplianceProfiles.Find(id));
            }
        }

        [Fact]
        public void PolicyProfilesPreserveVerifiedAuthorityBoundaries()
        {
            Assert.True(OdfComplianceProfiles.IsoIec26300_2015.SupportedVersions.Contains(OdfVersion.Odf12));
            Assert.False(OdfComplianceProfiles.IsoIec26300_2015.SupportedVersions.Contains(OdfVersion.Odf14));
            Assert.Equal(OdfPolicyAuthorityLevel.Normative, OdfComplianceProfiles.IsoIec26300_2015.AuthorityLevel);

            Assert.Equal(OdfPolicyAuthorityLevel.Compatibility, OdfComplianceProfiles.EuOfficeDocumentExchange.AuthorityLevel);
            Assert.Contains(
                OdfComplianceProfiles.EuOfficeDocumentExchange.Rules,
                rule => rule.Id == "AllowPolicyScopedOdfPreference");
            Assert.Equal(OdfPolicyAuthorityLevel.Compatibility, OdfComplianceProfiles.EuInteroperableEurope.AuthorityLevel);
            Assert.Equal(OdfProfileVerificationStatus.OfficialButIndirect, OdfComplianceProfiles.EuInteroperableEurope.VerificationStatus);

            Assert.Equal(OdfPolicyAuthorityLevel.Normative, OdfComplianceProfiles.RocTaiwanOdfCns15251.AuthorityLevel);
            Assert.Equal(OdfProfileVerificationStatus.VerifiedOfficial, OdfComplianceProfiles.RocTaiwanOdfCns15251.VerificationStatus);
            Assert.True(OdfComplianceProfiles.RocTaiwanOdfCns15251.SupportedVersions.Contains(OdfVersion.Odf12));
            Assert.False(OdfComplianceProfiles.RocTaiwanOdfCns15251.SupportedVersions.Contains(OdfVersion.Odf14));
            Assert.Equal(OdfPolicyAuthorityLevel.Compatibility, OdfComplianceProfiles.RocTaiwanGovernmentOdfTools.AuthorityLevel);
            Assert.Equal(OdfPolicyAuthorityLevel.Compatibility, OdfComplianceProfiles.UsNaraOdf.AuthorityLevel);
            Assert.Equal(OdfProfileVerificationStatus.OfficialButIndirect, OdfComplianceProfiles.UsNaraOdf.VerificationStatus);
            Assert.True(OdfComplianceProfiles.NlGovernmentOdf.SupportedVersions.Contains(OdfVersion.Odf12));
            Assert.False(OdfComplianceProfiles.NlGovernmentOdf.SupportedVersions.Contains(OdfVersion.Odf14));
        }

        [Fact]
        public void ProfileSemanticsKeepOasisSchemaValidationNormative()
        {
            Assert.Equal(OdfPolicyAuthorityLevel.Normative, OdfComplianceProfiles.OasisOdf14Strict.AuthorityLevel);
            Assert.Equal(OdfProfileVerificationStatus.VerifiedOfficial, OdfComplianceProfiles.OasisOdf14Strict.VerificationStatus);
            Assert.True(OdfComplianceProfiles.OasisOdf14Strict.SupportedVersions.Contains(OdfVersion.Odf14));
            Assert.False(OdfComplianceProfiles.OasisOdf14Strict.SupportedVersions.Contains(OdfVersion.Odf13));

            Assert.Equal(OdfPolicyAuthorityLevel.Normative, OdfComplianceProfiles.OasisOdf14Extended.AuthorityLevel);
            Assert.Equal(OdfProfileVerificationStatus.VerifiedOfficial, OdfComplianceProfiles.OasisOdf14Extended.VerificationStatus);
            Assert.True(OdfComplianceProfiles.OasisOdf14Extended.SupportedVersions.Contains(OdfVersion.Odf14));
            Assert.False(OdfComplianceProfiles.OasisOdf14Extended.SupportedVersions.Contains(OdfVersion.Odf13));

            Assert.Contains(
                OdfComplianceProfiles.OasisOdf14Strict.Rules,
                rule => rule.Id == "RequireSchemaPatternValidation" && rule.DefaultSeverity == OdfIssueSeverity.Error);
            Assert.Contains(
                OdfComplianceProfiles.OasisOdf14Extended.Rules,
                rule => rule.Id == "RequireSchemaPatternValidation" && rule.DefaultSeverity == OdfIssueSeverity.Error);
            Assert.Contains(
                OdfComplianceProfiles.OasisOdf14Strict.Rules,
                rule => rule.Id == "DisallowInvalidOdfNamespaceExtensions");
            Assert.DoesNotContain(
                OdfComplianceProfiles.OasisOdf14Extended.Rules,
                rule => rule.Id == "DisallowInvalidOdfNamespaceExtensions");
        }

        [Fact]
        public void ProfileSemanticsKeepDraftAndCompatibilityProfilesPolicyScoped()
        {
            OdfComplianceProfile[] policyProfiles =
            [
                OdfComplianceProfiles.EuInteroperableEurope,
                OdfComplianceProfiles.EuOfficeDocumentExchange,
                OdfComplianceProfiles.RocTaiwanGovernmentOdfTools,
                OdfComplianceProfiles.UsNaraOdf
            ];

            foreach (OdfComplianceProfile profile in policyProfiles)
            {
                Assert.NotEqual(OdfPolicyAuthorityLevel.Normative, profile.AuthorityLevel);
                Assert.NotEqual(OdfProfileVerificationStatus.VerifiedOfficial, profile.VerificationStatus);
                Assert.DoesNotContain(
                    profile.Rules,
                    rule => rule.Id == "DisallowInvalidOdfNamespaceExtensions");
            }

            Assert.Equal(OdfPolicyAuthorityLevel.Normative, OdfComplianceProfiles.RocTaiwanOdfCns15251.AuthorityLevel);
            Assert.Equal(OdfProfileVerificationStatus.VerifiedOfficial, OdfComplianceProfiles.RocTaiwanOdfCns15251.VerificationStatus);
            Assert.True(OdfComplianceProfiles.RocTaiwanOdfCns15251.SupportedVersions.Contains(OdfVersion.Odf12));
            Assert.False(OdfComplianceProfiles.RocTaiwanOdfCns15251.SupportedVersions.Contains(OdfVersion.Odf14));
            Assert.Equal(OdfPolicyAuthorityLevel.Compatibility, OdfComplianceProfiles.RocTaiwanGovernmentOdfTools.AuthorityLevel);
            Assert.Equal(OdfProfileVerificationStatus.CompatibilityOnly, OdfComplianceProfiles.RocTaiwanGovernmentOdfTools.VerificationStatus);
            Assert.Equal(OdfPolicyAuthorityLevel.Compatibility, OdfComplianceProfiles.EuOfficeDocumentExchange.AuthorityLevel);
            Assert.Equal(OdfProfileVerificationStatus.OfficialButIndirect, OdfComplianceProfiles.EuOfficeDocumentExchange.VerificationStatus);
            Assert.Equal(OdfPolicyAuthorityLevel.Compatibility, OdfComplianceProfiles.EuInteroperableEurope.AuthorityLevel);
            Assert.Equal(OdfProfileVerificationStatus.OfficialButIndirect, OdfComplianceProfiles.EuInteroperableEurope.VerificationStatus);
            Assert.Equal(OdfPolicyAuthorityLevel.Compatibility, OdfComplianceProfiles.UsNaraOdf.AuthorityLevel);
            Assert.Equal(OdfProfileVerificationStatus.OfficialButIndirect, OdfComplianceProfiles.UsNaraOdf.VerificationStatus);
        }

        [Fact]
        public void ProfileValidationReportsPolicyIssuesWithoutNormativeRuleIds()
        {
            string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" office:version=\"1.4\"><office:body><office:text><draw:frame><draw:image xlink:href=\"https://example.invalid/image.png\" /></draw:frame></office:text></office:body></office:document-content>";
            using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(
                package,
                OdfComplianceProfiles.RocTaiwanGovernmentOdfTools);

            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "RequireSafeExternalResourcePolicy" &&
                issue.ProfileId == OdfComplianceProfiles.RocTaiwanGovernmentOdfTools.Id &&
                issue.Severity == OdfIssueSeverity.Warning);
            Assert.DoesNotContain(report.Issues, issue =>
                issue.RuleId == "DisallowInvalidOdfNamespaceExtensions" &&
                issue.ProfileId == OdfComplianceProfiles.RocTaiwanGovernmentOdfTools.Id);
        }

        [Fact]
        public void SchemaRegistryExposesOdf14RootAndBodyMetadata()
        {
            OdfSchemaSet schema = OdfSchemaRegistry.GetSchema(OdfVersion.Odf14);

            Assert.Equal(OdfVersion.Odf14, schema.Version);
            Assert.Equal("2025-10-06", schema.SourceDate);

            OdfElementDefinition? root = schema.FindElement(
                OdfNamespaces.Office,
                "document-content");
            OdfElementDefinition? spreadsheet = schema.FindElement(
                OdfNamespaces.Office,
                "spreadsheet");
            OdfElementDefinition? paragraph = schema.FindElement(
                OdfNamespaces.Text,
                "p");
            OdfElementDefinition? table = schema.FindElement(
                OdfNamespaces.Table,
                "table");
            OdfElementDefinition? image = schema.FindElement(
                OdfNamespaces.Draw,
                "image");
            OdfAttributeDefinition? version = schema.FindAttribute(
                OdfNamespaces.Office,
                "version");
            OdfAttributeDefinition? mimetype = schema.FindAttribute(
                OdfNamespaces.Office,
                "mimetype");
            OdfAttributeDefinition? tableName = schema.FindAttribute(
                OdfNamespaces.Table,
                "name");

            Assert.NotNull(root);
            Assert.Equal(OdfSchemaElementRole.DocumentRoot, root!.Role);
            Assert.NotNull(spreadsheet);
            Assert.Equal(OdfSchemaElementRole.BodyContent, spreadsheet!.Role);
            Assert.Equal(OdfDocumentKind.Spreadsheet, spreadsheet.DocumentKind);
            Assert.NotNull(paragraph);
            Assert.NotNull(table);
            Assert.NotNull(image);
            Assert.NotNull(version);
            Assert.True(version!.IsRequiredOnDocumentRoot);
            Assert.Equal("odf-version", version.ValueType);
            Assert.NotNull(mimetype);
            Assert.Equal("media-type", mimetype!.ValueType);
            Assert.NotNull(tableName);
            Assert.Equal("string", tableName!.ValueType);
        }

        [Fact]
        public void SchemaSetMergeAddsGeneratedMetadataWithoutOverwritingSeedByDefault()
        {
            OdfSchemaSet seed = OdfSchemaRegistry.GetSchema(OdfVersion.Odf14);
            var generated = new OdfSchemaSet(
                OdfVersion.Odf14,
                new Uri("https://example.invalid/generated.rng"),
                "generated",
                new[]
                {
                    new OdfElementDefinition(
                        new OdfQualifiedName(OdfNamespaces.Text, "generated-element"),
                        OdfSchemaElementRole.Element,
                        OdfVersionRange.AllKnown),
                    new OdfElementDefinition(
                        new OdfQualifiedName(OdfNamespaces.Office, "spreadsheet"),
                        OdfSchemaElementRole.Element,
                        OdfVersionRange.AllKnown)
                },
                new[]
                {
                    new OdfAttributeDefinition(
                        new OdfQualifiedName(OdfNamespaces.Text, "generated-attribute"),
                        "string",
                        OdfVersionRange.AllKnown)
                },
                new[]
                {
                    new OdfSchemaNameClass(
                        OdfSchemaNameClassKind.NamespaceName,
                        OdfNamespaces.Text,
                        string.Empty,
                        isExcept: true)
                });

            OdfSchemaSet merged = seed.MergeWith(generated);

            Assert.NotNull(merged.FindElement(OdfNamespaces.Text, "generated-element"));
            Assert.NotNull(merged.FindAttribute(OdfNamespaces.Text, "generated-attribute"));
            Assert.Contains(merged.NameClasses, item =>
                item.Kind == OdfSchemaNameClassKind.NamespaceName &&
                item.NamespaceUri == OdfNamespaces.Text &&
                item.IsExcept);
            Assert.Equal(OdfSchemaElementRole.BodyContent, merged.FindElement(OdfNamespaces.Office, "spreadsheet")!.Role);
            Assert.Equal("https://example.invalid/generated.rng", merged.SourceUrl.ToString());
        }

        [Fact]
        public void NameClassMatcherEvaluatesFlatWildcardAndExceptMetadata()
        {
            var schema = new OdfSchemaSet(
                OdfVersion.Odf14,
                new Uri("https://example.invalid/generated.rng"),
                "generated",
                Array.Empty<OdfElementDefinition>(),
                nameClasses: new[]
                {
                    new OdfSchemaNameClass(
                        OdfSchemaNameClassKind.AnyName,
                        string.Empty,
                        string.Empty,
                        isExcept: false),
                    new OdfSchemaNameClass(
                        OdfSchemaNameClassKind.NamespaceName,
                        OdfNamespaces.Draw,
                        string.Empty,
                        isExcept: true),
                    new OdfSchemaNameClass(
                        OdfSchemaNameClassKind.Name,
                        OdfNamespaces.Text,
                        "span",
                        isExcept: true)
                });

            Assert.True(schema.NameClasses[0].Matches(OdfNamespaces.Text, "p"));
            Assert.True(schema.NameClasses[1].Matches(OdfNamespaces.Draw, "frame"));
            Assert.False(schema.NameClasses[1].Matches(OdfNamespaces.Text, "p"));
            Assert.True(schema.NameClasses[2].Matches(OdfNamespaces.Text, "span"));
            Assert.False(schema.NameClasses[2].Matches(OdfNamespaces.Text, "p"));

            Assert.Single(schema.FindMatchingNameClasses(OdfNamespaces.Text, "p"));
            Assert.Collection(
                schema.FindMatchingNameClasses(OdfNamespaces.Draw, "frame"),
                item => Assert.Equal(OdfSchemaNameClassKind.AnyName, item.Kind),
                item => Assert.Equal(OdfSchemaNameClassKind.NamespaceName, item.Kind));
            Assert.Collection(
                schema.FindMatchingNameClasses(OdfNamespaces.Text, "span"),
                item => Assert.Equal(OdfSchemaNameClassKind.AnyName, item.Kind),
                item => Assert.Equal(OdfSchemaNameClassKind.Name, item.Kind));
            Assert.True(schema.IsNameAllowedByNameClasses(OdfNamespaces.Text, "p"));
            Assert.True(schema.IsNameAllowedByNameClasses("urn:example:foreign", "thing"));
            Assert.False(schema.IsNameAllowedByNameClasses(OdfNamespaces.Draw, "frame"));
            Assert.False(schema.IsNameAllowedByNameClasses(OdfNamespaces.Text, "span"));
        }

        [Fact]
        public void SchemaSetPreservesAndMergesPatternTrees()
        {
            OdfSchemaSet seed = OdfSchemaRegistry.GetSchema(OdfVersion.Odf14);
            var generated = new OdfSchemaSet(
                OdfVersion.Odf14,
                new Uri("https://example.invalid/generated.rng"),
                "generated",
                Array.Empty<OdfElementDefinition>(),
                patterns: new[]
                {
                    new OdfSchemaPatternDefinition(
                        "root",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Office,
                                "document-content",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Ref,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        "paragraph",
                                        string.Empty,
                                        string.Empty)
                                })
                        })
                });

            OdfSchemaSet merged = seed.MergeWith(generated);
            OdfSchemaPatternDefinition? pattern = merged.FindPattern("root");

            Assert.NotNull(pattern);
            OdfSchemaPatternNode root = Assert.Single(pattern!.Roots);
            Assert.Equal(OdfSchemaPatternNodeKind.Element, root.Kind);
            Assert.Equal(OdfNamespaces.Office, root.NamespaceUri);
            Assert.Equal("document-content", root.LocalName);
            OdfSchemaPatternNode reference = Assert.Single(root.Children);
            Assert.Equal(OdfSchemaPatternNodeKind.Ref, reference.Kind);
            Assert.Equal("paragraph", reference.ReferenceName);
            Assert.Null(seed.FindPattern("root"));
        }

        [Fact]
        public void SchemaPatternValidatorMatchesElementsReferencesAndOccurrences()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement valid = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.4\"><text:p /><text:p /></office:document-content>");
            XElement validWhitespace = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.4\"> <text:p />\n<text:p /> </office:document-content>");
            XElement missingAttribute = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"><text:p /></office:document-content>");
            XElement wrongChild = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" office:version=\"1.4\"><draw:frame /></office:document-content>");
            XElement extraAttribute = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.4\" office:not-allowed=\"x\"><text:p /></office:document-content>");
            XElement extraText = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.4\">unexpected<text:p /></office:document-content>");

            OdfSchemaPatternValidationResult validResult = OdfSchemaPatternValidator.ValidateElement(valid, schema, "root");
            OdfSchemaPatternValidationResult validWhitespaceResult = OdfSchemaPatternValidator.ValidateElement(validWhitespace, schema, "root");
            OdfSchemaPatternValidationResult missingAttributeResult = OdfSchemaPatternValidator.ValidateElement(missingAttribute, schema, "root");
            OdfSchemaPatternValidationResult wrongChildResult = OdfSchemaPatternValidator.ValidateElement(wrongChild, schema, "root");
            OdfSchemaPatternValidationResult extraAttributeResult = OdfSchemaPatternValidator.ValidateElement(extraAttribute, schema, "root");
            OdfSchemaPatternValidationResult extraTextResult = OdfSchemaPatternValidator.ValidateElement(extraText, schema, "root");

            Assert.True(validResult.IsMatch);
            Assert.Empty(validResult.Issues);
            Assert.True(validWhitespaceResult.IsMatch);
            Assert.False(missingAttributeResult.IsMatch);
            Assert.Contains(missingAttributeResult.Issues, issue => issue.RuleId == "ODF3101");
            Assert.False(wrongChildResult.IsMatch);
            Assert.False(extraAttributeResult.IsMatch);
            Assert.False(extraTextResult.IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorHandlesChoiceAndNameClassExcept()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement text = XElement.Parse(
                "<office:text xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" />");
            XElement spreadsheet = XElement.Parse(
                "<office:spreadsheet xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" />");
            XElement textWithChild = XElement.Parse(
                "<office:text xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"><office:body /></office:text>");
            XElement allowedWildcard = XElement.Parse(
                "<text:p xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" />");
            XElement deniedWildcard = XElement.Parse(
                "<draw:frame xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" />");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(text, schema, "body-kind").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(spreadsheet, schema, "body-kind").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(textWithChild, schema, "body-kind").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(allowedWildcard, schema, "foreign-or-odf").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(deniedWildcard, schema, "foreign-or-odf").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorHandlesElementNameClassNodes()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement allowed = XElement.Parse(
                "<text:p xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" />");
            XElement denied = XElement.Parse(
                "<draw:frame xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" />");
            XElement invalidContent = XElement.Parse(
                "<text:p xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"><text:span /></text:p>");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(allowed, schema, "wildcard-empty-element").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(denied, schema, "wildcard-empty-element").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidContent, schema, "wildcard-empty-element").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorTreatsUnclassifiedStructuralWrappersAsTransparent()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement root = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"><text:p /></office:document-content>");
            XElement child = XElement.Parse(
                "<text:p xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" />");
            XElement invalid = XElement.Parse(
                "<draw:frame xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" />");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(root, schema, "other-wrapped-content").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(child, schema, "other-wrapped-root").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalid, schema, "other-wrapped-root").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorTreatsEmptyAsZeroWidthContent()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement valid = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"><text:p /></office:document-content>");
            XElement validWhitespace = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">  <text:p />  </office:document-content>");
            XElement unexpectedText = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">text<text:p /></office:document-content>");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(valid, schema, "empty-then-paragraph").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validWhitespace, schema, "empty-then-paragraph").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(unexpectedText, schema, "empty-then-paragraph").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorHandlesInterleaveChildrenOutOfOrder()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement declaredOrder = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\"><text:p /><table:table /></office:document-content>");
            XElement swappedOrder = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\"><table:table /><text:p /></office:document-content>");
            XElement missingRequiredChild = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"><text:p /></office:document-content>");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(declaredOrder, schema, "interleaved-content").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(swappedOrder, schema, "interleaved-content").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(missingRequiredChild, schema, "interleaved-content").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorHandlesRepeatedInterleaveChildren()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement paragraphsAroundTable = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\"><text:p /><table:table /><text:p /></office:document-content>");
            XElement tableBeforeParagraphs = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\"><table:table /><text:p /><text:p /></office:document-content>");
            XElement missingTable = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"><text:p /><text:p /></office:document-content>");
            XElement unexpectedChild = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\"><text:p /><draw:frame /><table:table /></office:document-content>");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(paragraphsAroundTable, schema, "interleaved-repeated-content").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(tableBeforeParagraphs, schema, "interleaved-repeated-content").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(missingTable, schema, "interleaved-repeated-content").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(unexpectedChild, schema, "interleaved-repeated-content").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorExpandsContentReferencesToFullPatterns()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement valid = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\"><text:p /><table:table /></office:document-content>");
            XElement missingSecond = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"><text:p /></office:document-content>");
            XElement wrongOrder = XElement.Parse(
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\"><table:table /><text:p /></office:document-content>");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(valid, schema, "ref-sequence-content").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(missingSecond, schema, "ref-sequence-content").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(wrongOrder, schema, "ref-sequence-content").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorExpandsWrappedStartPatterns()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement text = XElement.Parse(
                "<office:text xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" />");
            XElement spreadsheet = XElement.Parse(
                "<office:spreadsheet xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" />");
            XElement denied = XElement.Parse(
                "<draw:frame xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" />");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(text, schema, "start-group-wrapped-root").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(spreadsheet, schema, "start-group-wrapped-root").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(denied, schema, "start-group-wrapped-root").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorHandlesTextDataAndValueNodes()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement validInteger = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">42</text:sequence-ref>");
            XElement invalidInteger = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">not-a-number</text:sequence-ref>");
            XElement largeInteger = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">123456789012345678901234567890</text:sequence-ref>");
            XElement fractionalInteger = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">10.5</text:sequence-ref>");
            XElement validLiteral = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">approved</text:span>");
            XElement validCollapsedLiteral = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"> approved   </text:span>");
            XElement invalidLiteral = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">draft</text:span>");
            XElement validCollapsedToken = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"> two   words </text:sequence-ref>");
            XElement validStringLiteral = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">two  words</text:span>");
            XElement invalidCollapsedStringLiteral = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">two words</text:span>");
            XElement validDecimalLiteral = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">1.00</text:sequence-ref>");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(validInteger, schema, "integer-text").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidInteger, schema, "integer-text").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(largeInteger, schema, "integer-text").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(fractionalInteger, schema, "integer-text").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validLiteral, schema, "literal-value").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validCollapsedLiteral, schema, "literal-value").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidLiteral, schema, "literal-value").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validCollapsedToken, schema, "token-text").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validStringLiteral, schema, "string-literal-value").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidCollapsedStringLiteral, schema, "string-literal-value").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validDecimalLiteral, schema, "decimal-literal-value").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorRejectsUnsupportedDatatypeLibraries()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement validInteger = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">42</text:sequence-ref>");

            Assert.False(OdfSchemaPatternValidator.ValidateElement(validInteger, schema, "custom-datatype-library-text").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorRejectsUnknownDatatypes()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement text = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">42</text:sequence-ref>");

            Assert.False(OdfSchemaPatternValidator.ValidateElement(text, schema, "unknown-datatype-text").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorHandlesAttributeDataTypeNodes()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement valid = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" table:number-rows-repeated=\"3\" />");
            XElement invalid = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" table:number-rows-repeated=\"0\" />");
            XElement missing = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" />");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(valid, schema, "typed-attribute").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalid, schema, "typed-attribute").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(missing, schema, "typed-attribute").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorTreatsUnclassifiedAttributeWrappersAsTransparent()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement valid = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" table:number-rows-repeated=\"3\" />");
            XElement invalid = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" table:number-rows-repeated=\"0\" />");
            XElement missing = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" />");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(valid, schema, "other-wrapped-typed-attribute").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalid, schema, "other-wrapped-typed-attribute").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(missing, schema, "other-wrapped-typed-attribute").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorHandlesAttributeReferences()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement valid = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" table:number-rows-repeated=\"3\" />");
            XElement invalid = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" table:number-rows-repeated=\"0\" />");
            XElement missing = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" />");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(valid, schema, "attribute-ref-host").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalid, schema, "attribute-ref-host").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(missing, schema, "attribute-ref-host").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorHandlesOptionalAttributeValueNodes()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement missing = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" />");
            XElement valid = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" table:number-rows-repeated=\"2\" />");
            XElement invalid = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" table:number-rows-repeated=\"0\" />");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(missing, schema, "optional-typed-attribute").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(valid, schema, "optional-typed-attribute").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalid, schema, "optional-typed-attribute").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorTreatsChoiceAttributesAsExclusive()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement repeated = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" table:number-rows-repeated=\"2\" />");
            XElement named = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" table:name=\"Sheet1\" />");
            XElement both = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" table:number-rows-repeated=\"2\" table:name=\"Sheet1\" />");
            XElement missing = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" />");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(repeated, schema, "choice-attribute").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(named, schema, "choice-attribute").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(both, schema, "choice-attribute").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(missing, schema, "choice-attribute").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorHandlesNameClassAttributeNodes()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement valid = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" table:number-rows-repeated=\"2\" />");
            XElement invalid = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" table:name=\"Sheet1\" />");
            XElement missing = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" />");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(valid, schema, "wildcard-positive-integer-attribute").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalid, schema, "wildcard-positive-integer-attribute").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(missing, schema, "wildcard-positive-integer-attribute").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorHandlesMixedContentNodes()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement valid = XElement.Parse(
                "<text:p xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">before <text:span /> after</text:p>");
            XElement textOnly = XElement.Parse(
                "<text:p xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">plain text</text:p>");
            XElement invalidChild = XElement.Parse(
                "<text:p xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\"><draw:frame /></text:p>");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(valid, schema, "mixed-paragraph").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(textOnly, schema, "mixed-paragraph").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidChild, schema, "mixed-paragraph").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorDoesNotTreatTextChoiceAsMixedContent()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement textOnly = XElement.Parse(
                "<text:p xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">plain text</text:p>");
            XElement childOnly = XElement.Parse(
                "<text:p xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"><text:span /></text:p>");
            XElement textAndChild = XElement.Parse(
                "<text:p xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">before <text:span /></text:p>");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(textOnly, schema, "text-or-span-choice").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(childOnly, schema, "text-or-span-choice").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(textAndChild, schema, "text-or-span-choice").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorHandlesListValueNodes()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement valid = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">1 2 3</text:span>");
            XElement invalid = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">1 two 3</text:span>");
            XElement empty = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" />");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(valid, schema, "integer-list").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalid, schema, "integer-list").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(empty, schema, "integer-list").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorAppliesDatatypeParameters()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement valid = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">5</text:sequence-ref>");
            XElement tooSmall = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">0</text:sequence-ref>");
            XElement tooLarge = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">11</text:sequence-ref>");
            XElement validLargeDecimal = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">123456789012345678901234567890.25</text:sequence-ref>");
            XElement tooLargeDecimal = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">123456789012345678901234567891</text:sequence-ref>");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(valid, schema, "bounded-integer-text").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(tooSmall, schema, "bounded-integer-text").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(tooLarge, schema, "bounded-integer-text").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validLargeDecimal, schema, "bounded-decimal-text").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(tooLargeDecimal, schema, "bounded-decimal-text").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorAppliesDataExceptNodes()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement validText = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">5</text:sequence-ref>");
            XElement deniedText = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">7</text:sequence-ref>");
            XElement validAttribute = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" table:number-rows-repeated=\"2\" />");
            XElement deniedAttribute = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" table:number-rows-repeated=\"1\" />");
            XElement validList = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">1 2 3</text:span>");
            XElement deniedList = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">1 0 3</text:span>");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(validText, schema, "integer-text-except-seven").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(deniedText, schema, "integer-text-except-seven").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validAttribute, schema, "positive-attribute-except-one").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(deniedAttribute, schema, "positive-attribute-except-one").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validList, schema, "integer-list-except-zero").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(deniedList, schema, "integer-list-except-zero").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorRejectsNotAllowedPatterns()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement text = XElement.Parse(
                "<text:p xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">blocked</text:p>");
            XElement attribute = XElement.Parse(
                "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" table:number-rows-repeated=\"2\" />");
            XElement list = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">1 2</text:span>");

            Assert.False(OdfSchemaPatternValidator.ValidateElement(text, schema, "not-allowed-content").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(attribute, schema, "not-allowed-attribute-value").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(list, schema, "not-allowed-list-token").IsMatch);
        }

        [Fact]
        public void SchemaPatternValidatorHandlesAdditionalXmlSchemaDatatypes()
        {
            OdfSchemaSet schema = CreatePatternValidationSchema();
            XElement validDuration = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">P1DT2H</text:sequence-ref>");
            XElement invalidDuration = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">not-duration</text:sequence-ref>");
            XElement validTime = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">13:20:00Z</text:sequence-ref>");
            XElement invalidTime = XElement.Parse(
                "<text:sequence-ref xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">P1DT2H</text:sequence-ref>");
            XElement validUri = XElement.Parse(
                "<draw:image xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xlink:href=\"Pictures/image.png\" />");
            XElement invalidUri = XElement.Parse(
                "<draw:image xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xlink:href=\"bad uri\" />");
            XElement validNCName = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">Style_1</text:span>");
            XElement invalidNCName = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">bad:name</text:span>");
            XElement validQName = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">style:family</text:span>");
            XElement invalidQName = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">style:bad:name</text:span>");
            XElement validHexBinary = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">0A1f</text:span>");
            XElement invalidHexBinary = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">0A1</text:span>");
            XElement validBase64Binary = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">AQID</text:span>");
            XElement invalidBase64Binary = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">not-base64?</text:span>");
            XElement validIdRefs = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">id1 id_2</text:span>");
            XElement invalidIdRefs = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">id1 bad:name</text:span>");
            XElement validNmTokens = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">name:part value.2</text:span>");
            XElement invalidNmTokens = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">name:part bad token?</text:span>");
            XElement validNonNegativeInteger = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">123456789012345678901234567890</text:span>");
            XElement invalidNonNegativeInteger = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">-1</text:span>");
            XElement validNormalizedString = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">two words</text:span>");
            XElement invalidNormalizedString = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">two&#x9;words</text:span>");
            XElement validDecimal = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">123456789012345678901234567890.123456789</text:span>");
            XElement invalidDecimalExponent = XElement.Parse(
                "<text:span xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">1e3</text:span>");

            Assert.True(OdfSchemaPatternValidator.ValidateElement(validDuration, schema, "duration-text").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidDuration, schema, "duration-text").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validTime, schema, "time-text").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidTime, schema, "time-text").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validUri, schema, "uri-attribute").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidUri, schema, "uri-attribute").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validNCName, schema, "ncname-text").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidNCName, schema, "ncname-text").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validQName, schema, "qname-text").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidQName, schema, "qname-text").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validHexBinary, schema, "hex-binary-text").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidHexBinary, schema, "hex-binary-text").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validBase64Binary, schema, "base64-binary-text").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidBase64Binary, schema, "base64-binary-text").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validIdRefs, schema, "idrefs-text").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidIdRefs, schema, "idrefs-text").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validNmTokens, schema, "nmtokens-text").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidNmTokens, schema, "nmtokens-text").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validNonNegativeInteger, schema, "nonnegative-integer-text").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidNonNegativeInteger, schema, "nonnegative-integer-text").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validNormalizedString, schema, "normalized-string-text").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidNormalizedString, schema, "normalized-string-text").IsMatch);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(validDecimal, schema, "decimal-text").IsMatch);
            Assert.False(OdfSchemaPatternValidator.ValidateElement(invalidDecimalExponent, schema, "decimal-text").IsMatch);
        }

        [Fact]
        public void SchemaRegistryUsesOfficialGeneratedProvider()
        {
            OdfSchemaSet schema = OdfSchemaRegistry.GetSchema(OdfVersion.Odf14);
            OdfSchemaSet provided = OdfGeneratedSchemaProvider.CreateOdf14(schema);

            Assert.NotSame(schema, provided);
            Assert.Equal(OdfVersion.Odf14, provided.Version);
            Assert.Equal(
                "https://docs.oasis-open.org/office/OpenDocument/v1.4/os/schemas/OpenDocument-v1.4-schema.rng",
                provided.SourceUrl.ToString());
            Assert.Equal("2025-10-06", provided.SourceDate);
            Assert.NotNull(provided.FindPattern("start"));
        }

        [Fact]
        public void SchemaRegistryRegistrationFeedsValidatorsAndRestoresPreviousSchema()
        {
            using MemoryStream ms = CreatePackage(
                "application/vnd.oasis.opendocument.text",
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.4\"><office:body><office:text><text:generated-element /></office:text></office:body></office:document-content>");
            var generated = new OdfSchemaSet(
                OdfVersion.Odf14,
                new Uri("https://example.invalid/generated.rng"),
                "generated",
                new[]
                {
                    new OdfElementDefinition(
                        new OdfQualifiedName(OdfNamespaces.Text, "generated-element"),
                        OdfSchemaElementRole.Element,
                        OdfVersionRange.AllKnown)
                });

            using (OdfPackage package = OdfPackage.Open(ms, leaveOpen: true))
            {
                OdfValidationReport before = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);
                Assert.False(before.IsValid);
                Assert.Contains(before.Issues, issue => issue.RuleId == "DisallowInvalidOdfNamespaceExtensions");
            }

            ms.Position = 0;
            using (OdfSchemaRegistry.RegisterSchema(generated, overwriteExisting: true))
            using (OdfPackage package = OdfPackage.Open(ms, leaveOpen: true))
            {
                OdfValidationReport registered = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);
                Assert.True(registered.IsValid, string.Join(", ", registered.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
                Assert.NotNull(OdfSchemaRegistry.GetSchema(OdfVersion.Odf14).FindElement(OdfNamespaces.Text, "generated-element"));
            }

            ms.Position = 0;
            using (OdfPackage package = OdfPackage.Open(ms))
            {
                OdfValidationReport after = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);
                Assert.False(after.IsValid);
                Assert.Null(OdfSchemaRegistry.GetSchema(OdfVersion.Odf14).FindElement(OdfNamespaces.Text, "generated-element"));
            }
        }

        [Fact]
        public void PackageValidatorAppliesOptInSchemaPatternValidation()
        {
            OdfSchemaSet generated = CreateRootPatternSchema("document-content", "mimetype");
            OdfComplianceProfile profile = CreateSchemaPatternValidationProfile();
            string validXml =
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\" office:mimetype=\"application/vnd.oasis.opendocument.text\"><office:body><office:text /></office:body></office:document-content>";
            string invalidXml =
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:body><office:text /></office:body></office:document-content>";

            using (OdfSchemaRegistry.RegisterSchema(generated, overwriteExisting: true))
            {
                using MemoryStream validStream = CreatePackage("application/vnd.oasis.opendocument.text", validXml);
                using OdfPackage validPackage = OdfPackage.Open(validStream);
                OdfValidationReport validReport = OdfPackageValidator.Validate(validPackage, profile);

                Assert.True(validReport.IsValid, string.Join(", ", validReport.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
            }
            using (OdfSchemaRegistry.RegisterSchema(generated, overwriteExisting: true))
            {
                using MemoryStream invalidStream = CreatePackage("application/vnd.oasis.opendocument.text", invalidXml);
                using OdfPackage invalidPackage = OdfPackage.Open(invalidStream);
                OdfValidationReport invalidReport = OdfPackageValidator.Validate(invalidPackage, profile);

                Assert.False(invalidReport.IsValid);
                Assert.Contains(invalidReport.Issues, issue =>
                    issue.RuleId == "ODF3101" &&
                    issue.ProfileId == profile.Id &&
                    issue.PackagePath == "content.xml");
            }
        }

        [Fact]
        public void PackageValidatorUsesGeneratedStartPatternForSchemaValidation()
        {
            OdfSchemaSet generated = CreateStartPatternSchema("document-content", "mimetype");
            OdfComplianceProfile profile = CreateSchemaPatternValidationProfile();
            string validXml =
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\" office:mimetype=\"application/vnd.oasis.opendocument.text\"><office:body><office:text /></office:body></office:document-content>";
            string invalidXml =
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:body><office:text /></office:body></office:document-content>";

            using (OdfSchemaRegistry.RegisterSchema(generated, overwriteExisting: true))
            {
                using MemoryStream validStream = CreatePackage("application/vnd.oasis.opendocument.text", validXml);
                using OdfPackage validPackage = OdfPackage.Open(validStream);
                OdfValidationReport validReport = OdfPackageValidator.Validate(validPackage, profile);

                Assert.True(validReport.IsValid, string.Join(", ", validReport.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
            }

            using (OdfSchemaRegistry.RegisterSchema(generated, overwriteExisting: true))
            {
                using MemoryStream invalidStream = CreatePackage("application/vnd.oasis.opendocument.text", invalidXml);
                using OdfPackage invalidPackage = OdfPackage.Open(invalidStream);
                OdfValidationReport invalidReport = OdfPackageValidator.Validate(invalidPackage, profile);

                Assert.False(invalidReport.IsValid);
                Assert.Contains(invalidReport.Issues, issue =>
                    issue.RuleId == "ODF3101" &&
                    issue.ProfileId == profile.Id &&
                    issue.PackagePath == "content.xml");
            }
        }

        [Fact]
        public void PackageValidatorFallsBackFromStartPatternToPackageEntryPattern()
        {
            OdfSchemaSet generated = CreatePackageEntryFallbackSchema();
            OdfComplianceProfile profile = CreateSchemaPatternValidationProfile();
            string contentXml =
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\" office:mimetype=\"application/vnd.oasis.opendocument.text\"><office:body><office:text /></office:body></office:document-content>";
            string stylesXml =
                "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\" />";

            using IDisposable registration = OdfSchemaRegistry.RegisterSchema(generated, overwriteExisting: true);
            using MemoryStream stream = CreatePackage(
                "application/vnd.oasis.opendocument.text",
                contentXml,
                new[] { new KeyValuePair<string, string>("styles.xml", stylesXml) });
            using OdfPackage package = OdfPackage.Open(stream);
            OdfValidationReport report = OdfPackageValidator.Validate(package, profile);

            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
        }

        [Fact]
        public void PackageValidatorAppliesSchemaPatternValidationToEmbeddedObjectEntries()
        {
            OdfSchemaSet generated = CreateRootPatternSchema("document-content", "mimetype");
            OdfComplianceProfile profile = CreateSchemaPatternValidationProfile();
            string contentXml =
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\" office:mimetype=\"application/vnd.oasis.opendocument.text\"><office:body><office:text /></office:body></office:document-content>";
            string embeddedXml =
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:body><office:text /></office:body></office:document-content>";

            using IDisposable registration = OdfSchemaRegistry.RegisterSchema(generated, overwriteExisting: true);
            using MemoryStream stream = CreatePackage(
                "application/vnd.oasis.opendocument.text",
                contentXml,
                new[] { new KeyValuePair<string, string>("Object 1/content.xml", embeddedXml) });
            using OdfPackage package = OdfPackage.Open(stream);
            OdfValidationReport report = OdfPackageValidator.Validate(package, profile);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF3101" &&
                issue.ProfileId == profile.Id &&
                issue.PackagePath == "Object 1/content.xml");
        }

        [Fact]
        public void PackageValidatorScansEmbeddedObjectEntriesForProfileRules()
        {
            string contentXml =
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:body><office:text /></office:body></office:document-content>";
            string embeddedXml =
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.4\"><office:body><office:text><text:not-a-real-element /></office:text></office:body></office:document-content>";

            using MemoryStream stream = CreatePackage(
                "application/vnd.oasis.opendocument.text",
                contentXml,
                new[] { new KeyValuePair<string, string>("Object 1/content.xml", embeddedXml) });
            using OdfPackage package = OdfPackage.Open(stream);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "DisallowInvalidOdfNamespaceExtensions" &&
                issue.PackagePath == "Object 1/content.xml");
        }

        [Fact]
        public void PackageValidatorChecksEmbeddedObjectXmlRootNames()
        {
            string contentXml =
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:body><office:text /></office:body></office:document-content>";
            string embeddedXml =
                "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\" />";

            using MemoryStream stream = CreatePackage(
                "application/vnd.oasis.opendocument.text",
                contentXml,
                new[] { new KeyValuePair<string, string>("Object 1/content.xml", embeddedXml) });
            using OdfPackage package = OdfPackage.Open(stream);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF0300" &&
                issue.PackagePath == "Object 1/content.xml");
        }

        [Fact]
        public void ValidatorDetectsOdf14TextPackage()
        {
            using MemoryStream ms = CreatePackage(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4"));

            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
            Assert.Equal(OdfVersion.Odf14, report.DetectedVersion);
            Assert.Equal(OdfDocumentKind.Text, report.DocumentKind);
        }

        [Fact]
        public void ValidatorReportsProfileDisallowedPackageExtension()
        {
            using MemoryStream ms = CreatePackage(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4"));

            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(
                package,
                OdfComplianceProfiles.OasisOdf14Strict,
                "document.zip");

            Assert.False(report.IsValid);
            Assert.Equal(OdfDocumentKind.Text, report.DocumentKind);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF1002" &&
                issue.ProfileId == OdfComplianceProfiles.OasisOdf14Strict.Id);
        }

        [Fact]
        public void ValidatorReportsPackageExtensionAndMimeTypeMismatch()
        {
            using MemoryStream ms = CreatePackage(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4"));

            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(
                package,
                OdfComplianceProfiles.OasisOdf14Strict,
                "workbook.ods");

            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
            Assert.Equal(OdfDocumentKind.Text, report.DocumentKind);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF0501" &&
                issue.PackagePath == "workbook.ods");
        }

        [Fact]
        public void ValidatorRejectsOdf14ForIso26300Odf12Profile()
        {
            using MemoryStream ms = CreatePackage(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4"));

            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.IsoIec26300_2015);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF1001");
        }

        [Fact]
        public void ValidatorReportsMissingManifest()
        {
            using MemoryStream ms = CreateZipWithoutManifest();
            using OdfPackage package = OdfPackage.Open(ms, options: new OdfLoadOptions { ValidateMimeType = false });

            OdfValidationReport report = OdfPackageValidator.Validate(package);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF0100");
        }

        [Fact]
        public void ValidatorReportsMimetypeEntryNotFirst()
        {
            using MemoryStream ms = CreateZipWithMimetypeLayout(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4"),
                mimetypeFirst: false,
                CompressionLevel.NoCompression);
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF0003" &&
                issue.PackagePath == "mimetype");
        }

        [Fact]
        public void ValidatorReportsCompressedMimetypeEntry()
        {
            using MemoryStream ms = CreateZipWithMimetypeLayout(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4"),
                mimetypeFirst: true,
                CompressionLevel.Fastest);
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF0004" &&
                issue.PackagePath == "mimetype");
        }

        [Fact]
        public void ValidatorReportsDuplicatePackageEntries()
        {
            using MemoryStream ms = CreateZipWithDuplicateContentEntry(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4"));
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF0202" &&
                issue.PackagePath == "content.xml");
        }

        [Fact]
        public void ValidatorReportsManifestRootMimetypeMismatch()
        {
            using MemoryStream ms = CreateZipWithManifestRootMediaType(
                "application/vnd.oasis.opendocument.text",
                "application/vnd.oasis.opendocument.spreadsheet",
                CreateDocumentContent("1.4"));
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            OdfValidationIssue issue = Assert.Single(report.Issues, issue =>
                issue.RuleId == "ODF0103" &&
                issue.PackagePath == "META-INF/manifest.xml");
            Assert.Equal("/", issue.Details["entryPath"]);
            Assert.Equal("application/vnd.oasis.opendocument.text", issue.Details["expectedMediaType"]);
            Assert.Equal("application/vnd.oasis.opendocument.spreadsheet", issue.Details["actualMediaType"]);
        }

        [Fact]
        public void ValidatorReportsManifestEntriesWithoutPackagePayload()
        {
            using MemoryStream ms = CreateZipWithCustomManifestEntries(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4"),
                new[]
                {
                    new KeyValuePair<string, string>("Object 1/", "application/vnd.oasis.opendocument.text"),
                    new KeyValuePair<string, string>("Pictures/missing.png", "image/png")
                });
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF0104" &&
                issue.PackagePath == "Pictures/missing.png");
            Assert.DoesNotContain(report.Issues, issue =>
                issue.RuleId == "ODF0104" &&
                issue.PackagePath == "Object 1/");
        }

        [Fact]
        public void ValidatorReportsDuplicateManifestFullPaths()
        {
            using MemoryStream ms = CreateZipWithCustomManifestEntries(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4"),
                new[]
                {
                    new KeyValuePair<string, string>("content.xml", "text/xml")
                });
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF0107" &&
                issue.PackagePath == "content.xml");
        }

        [Fact]
        public void ValidatorReportsManifestFileEntryMissingRequiredAttributes()
        {
            using MemoryStream ms = CreateZipWithRawManifest(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4"),
                "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.4\">" +
                "<manifest:file-entry manifest:media-type=\"application/vnd.oasis.opendocument.text\" />" +
                "<manifest:file-entry manifest:full-path=\"content.xml\" />" +
                "</manifest:manifest>");
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF0108" &&
                issue.PackagePath == "META-INF/manifest.xml");
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF0109" &&
                issue.PackagePath == "content.xml");
        }

        [Fact]
        public void ValidatorReportsUnsafeManifestFullPathWithoutFailingLoad()
        {
            using MemoryStream ms = CreateZipWithRawManifest(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4"),
                "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.4\">" +
                "<manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.text\" />" +
                "<manifest:file-entry manifest:full-path=\"../content.xml\" manifest:media-type=\"text/xml\" />" +
                "</manifest:manifest>");

            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF0201" &&
                issue.PackagePath == "../content.xml");
        }

        [Fact]
        public void ValidatorReportsUnexpectedManifestRoot()
        {
            using MemoryStream ms = CreateZipWithRawManifest(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4"),
                "<manifest xmlns=\"urn:example:wrong\" version=\"1.4\">" +
                "<manifest:file-entry xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.text\" />" +
                "<manifest:file-entry xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\" />" +
                "</manifest>");
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF0111" &&
                issue.PackagePath == "META-INF/manifest.xml");
        }

        [Fact]
        public void ValidatorReportsMissingManifestVersion()
        {
            using MemoryStream ms = CreateZipWithRawManifest(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4"),
                "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\">" +
                "<manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.text\" />" +
                "<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\" />" +
                "</manifest:manifest>");
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF0112" &&
                issue.PackagePath == "META-INF/manifest.xml");
        }

        [Fact]
        public void ValidatorReportsDirectoryManifestEntryWithoutTrailingSlash()
        {
            using MemoryStream ms = CreateZipWithCustomManifestEntries(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4"),
                new[]
                {
                    new KeyValuePair<string, string>("Object 1", string.Empty)
                });
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF0106" &&
                issue.PackagePath == "Object 1");
        }

        [Fact]
        public void ValidatorReportsCoreXmlManifestMediaTypeMismatch()
        {
            using MemoryStream ms = CreateZipWithCustomManifestEntries(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4"),
                Array.Empty<KeyValuePair<string, string>>(),
                contentMediaType: "image/png");
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            OdfValidationIssue issue = Assert.Single(report.Issues, issue =>
                issue.RuleId == "ODF0105" &&
                issue.PackagePath == "content.xml");
            Assert.Equal("content.xml", issue.Details["entryPath"]);
            Assert.Equal("text/xml", issue.Details["expectedMediaType"]);
            Assert.Equal("image/png", issue.Details["actualMediaType"]);
        }

        [Fact]
        public void ValidatorReportsIncompleteEncryptionMetadata()
        {
            using MemoryStream ms = CreateZipWithRawManifest(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4"),
                "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.4\">" +
                "<manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.text\" />" +
                "<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\">" +
                "<manifest:encryption-data manifest:checksum-type=\"SHA256\" manifest:checksum=\"AQID\" />" +
                "</manifest:file-entry>" +
                "</manifest:manifest>");
            using OdfPackage package = OdfPackage.Open(ms);

            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF0120" &&
                issue.PackagePath == "content.xml" &&
                issue.Message.Contains("algorithm-name", StringComparison.Ordinal) &&
                issue.Message.Contains("key-derivation-name", StringComparison.Ordinal));
        }

        [Fact]
        public void ValidatorReportsMissingDeclaredVersion()
        {
            using MemoryStream ms = CreatePackage(
                "application/vnd.oasis.opendocument.text",
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" />");

            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.Equal(OdfVersion.Unknown, report.DetectedVersion);
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF0400");
        }

        [Theory]
        [InlineData("example.odt", OdfDocumentKind.Text)]
        [InlineData("example.ott", OdfDocumentKind.TextTemplate)]
        [InlineData("example.odm", OdfDocumentKind.TextMaster)]
        [InlineData("example.ods", OdfDocumentKind.Spreadsheet)]
        [InlineData("example.ots", OdfDocumentKind.SpreadsheetTemplate)]
        [InlineData("example.odp", OdfDocumentKind.Presentation)]
        [InlineData("example.otp", OdfDocumentKind.PresentationTemplate)]
        [InlineData("example.odg", OdfDocumentKind.Graphics)]
        [InlineData("example.otg", OdfDocumentKind.GraphicsTemplate)]
        [InlineData("example.odc", OdfDocumentKind.Chart)]
        [InlineData("example.odf", OdfDocumentKind.Formula)]
        [InlineData("example.odi", OdfDocumentKind.Image)]
        [InlineData("example.odb", OdfDocumentKind.Database)]
        [InlineData("example.fodt", OdfDocumentKind.FlatText)]
        [InlineData("example.fods", OdfDocumentKind.FlatSpreadsheet)]
        [InlineData("example.fodp", OdfDocumentKind.FlatPresentation)]
        [InlineData("example.fodg", OdfDocumentKind.FlatGraphics)]
        [InlineData("example.fodc", OdfDocumentKind.FlatChart)]
        [InlineData("example.fdf", OdfDocumentKind.FlatFormula)]
        [InlineData("example.fodi", OdfDocumentKind.FlatImage)]
        [InlineData("example.oth", OdfDocumentKind.TextWeb)]
        [InlineData("example.otc", OdfDocumentKind.ChartTemplate)]
        [InlineData("example.otf", OdfDocumentKind.FormulaTemplate)]
        [InlineData("example.oti", OdfDocumentKind.ImageTemplate)]
        public void DocumentKindDetectorRecognizesOdfExtensions(string fileName, OdfDocumentKind expected)
        {
            Assert.Equal(expected, OdfDocumentKindDetector.FromFileName(fileName));
        }

        [Fact]
        public void DocumentKindDetectorExposesSupportedFormatMetadata()
        {
            Assert.Equal(24, OdfDocumentKindDetector.SupportedFormats.Count);

            Assert.True(OdfDocumentKindDetector.TryGetFormatByFileName(".fods", out OdfFormatInfo? flatSpreadsheet));
            Assert.Equal(OdfDocumentKind.FlatSpreadsheet, flatSpreadsheet!.Kind);
            Assert.Equal(OdfDocumentKind.Spreadsheet, flatSpreadsheet.BodyKind);
            Assert.True(flatSpreadsheet.IsFlatXml);
            Assert.False(flatSpreadsheet.IsTemplate);
            Assert.False(flatSpreadsheet.IsMasterDocument);
            Assert.Equal("application/vnd.oasis.opendocument.spreadsheet", flatSpreadsheet.MimeType);

            Assert.True(OdfDocumentKindDetector.TryGetFormatByKind(OdfDocumentKind.PresentationTemplate, out OdfFormatInfo? presentationTemplate));
            Assert.Equal(".otp", presentationTemplate!.Extension);
            Assert.True(presentationTemplate.IsTemplate);
            Assert.False(presentationTemplate.IsMasterDocument);
            Assert.False(presentationTemplate.IsFlatXml);

            Assert.True(OdfDocumentKindDetector.TryGetFormatByKind(OdfDocumentKind.TextMaster, out OdfFormatInfo? textMaster));
            Assert.Equal(".odm", textMaster!.Extension);
            Assert.False(textMaster.IsTemplate);
            Assert.True(textMaster.IsMasterDocument);
            Assert.False(textMaster.IsFlatXml);

            Assert.True(OdfDocumentKindDetector.TryGetFormatByKind(OdfDocumentKind.Database, out OdfFormatInfo? database));
            Assert.Equal(".odb", database!.Extension);
            Assert.Equal("application/vnd.oasis.opendocument.base", database.MimeType);
            Assert.False(database.IsFlatXml);
            Assert.False(database.IsTemplate);
            Assert.False(database.IsMasterDocument);
        }

        [Theory]
        [InlineData("application/vnd.oasis.opendocument.text", OdfDocumentKind.Text)]
        [InlineData("application/vnd.oasis.opendocument.text-template", OdfDocumentKind.TextTemplate)]
        [InlineData("application/vnd.oasis.opendocument.text-master", OdfDocumentKind.TextMaster)]
        [InlineData("application/vnd.oasis.opendocument.spreadsheet", OdfDocumentKind.Spreadsheet)]
        [InlineData("application/vnd.oasis.opendocument.spreadsheet-template", OdfDocumentKind.SpreadsheetTemplate)]
        [InlineData("application/vnd.oasis.opendocument.presentation", OdfDocumentKind.Presentation)]
        [InlineData("application/vnd.oasis.opendocument.presentation-template", OdfDocumentKind.PresentationTemplate)]
        [InlineData("application/vnd.oasis.opendocument.graphics", OdfDocumentKind.Graphics)]
        [InlineData("application/vnd.oasis.opendocument.graphics-template", OdfDocumentKind.GraphicsTemplate)]
        [InlineData("application/vnd.oasis.opendocument.chart", OdfDocumentKind.Chart)]
        [InlineData("application/vnd.oasis.opendocument.formula", OdfDocumentKind.Formula)]
        [InlineData("application/vnd.oasis.opendocument.image", OdfDocumentKind.Image)]
        [InlineData("application/vnd.oasis.opendocument.base", OdfDocumentKind.Database)]
        public void DocumentKindDetectorRecognizesOdfMimeTypes(string mimeType, OdfDocumentKind expected)
        {
            Assert.Equal(expected, OdfDocumentKindDetector.FromMimeType(mimeType));
            Assert.True(OdfDocumentKindDetector.TryGetFormatByMimeType(mimeType, out OdfFormatInfo? format));
            Assert.Equal(expected, format!.Kind);
        }

        [Theory]
        [InlineData("text", false, OdfDocumentKind.Text)]
        [InlineData("spreadsheet", false, OdfDocumentKind.Spreadsheet)]
        [InlineData("presentation", false, OdfDocumentKind.Presentation)]
        [InlineData("drawing", false, OdfDocumentKind.Graphics)]
        [InlineData("chart", false, OdfDocumentKind.Chart)]
        [InlineData("formula", false, OdfDocumentKind.Formula)]
        [InlineData("image", false, OdfDocumentKind.Image)]
        [InlineData("database", false, OdfDocumentKind.Database)]
        [InlineData("text", true, OdfDocumentKind.FlatText)]
        [InlineData("spreadsheet", true, OdfDocumentKind.FlatSpreadsheet)]
        [InlineData("presentation", true, OdfDocumentKind.FlatPresentation)]
        [InlineData("drawing", true, OdfDocumentKind.FlatGraphics)]
        [InlineData("chart", true, OdfDocumentKind.FlatChart)]
        [InlineData("formula", true, OdfDocumentKind.FlatFormula)]
        [InlineData("image", true, OdfDocumentKind.FlatImage)]
        public void DocumentKindDetectorRecognizesOfficeBodyKinds(string localName, bool flat, OdfDocumentKind expected)
        {
            Assert.Equal(expected, OdfDocumentKindDetector.FromOfficeBodyElement(localName, flat));
        }

        [Fact]
        public void ValidatorDetectsTemplatePackageKind()
        {
            using MemoryStream ms = CreatePackage(
                "application/vnd.oasis.opendocument.spreadsheet-template",
                CreateDocumentContent("1.4"));

            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package);

            Assert.Equal(OdfDocumentKind.SpreadsheetTemplate, report.DocumentKind);
        }

        [Fact]
        public void ValidatorReportsPackageMimeTypeAndBodyMismatch()
        {
            using MemoryStream ms = CreatePackage(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4", "spreadsheet"));

            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package);

            Assert.False(report.IsValid);
            Assert.Equal(OdfDocumentKind.Text, report.DocumentKind);
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF0500");
        }

        [Fact]
        public void ValidatorRejectsUnknownOdfBodyElement()
        {
            using MemoryStream ms = CreatePackage(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4", "not-a-document-kind"));

            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3002");
        }

        [Fact]
        public void StrictProfileReportsUnknownOdfNamespaceElement()
        {
            using MemoryStream ms = CreatePackage(
                "application/vnd.oasis.opendocument.text",
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.4\"><office:body><office:text><text:unknown /></office:text></office:body></office:document-content>");

            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue => issue.RuleId == "DisallowInvalidOdfNamespaceExtensions");
        }

        [Fact]
        public void StrictProfileAllowsSeededCommonOdfElementsAndAttributes()
        {
            using MemoryStream ms = CreatePackage(
                "application/vnd.oasis.opendocument.spreadsheet",
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" office:version=\"1.4\"><office:body><office:spreadsheet><table:table table:name=\"Sheet1\"><table:table-column /><table:table-row><table:table-cell office:value-type=\"string\" office:string-value=\"A1\" /></table:table-row></table:table></office:spreadsheet></office:body></office:document-content>");

            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
        }

        [Fact]
        public void StrictProfileReportsUnknownOdfNamespaceAttribute()
        {
            using MemoryStream ms = CreatePackage(
                "application/vnd.oasis.opendocument.text",
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.4\"><office:body><office:text><text:p text:not-a-real-attribute=\"x\" /></office:text></office:body></office:document-content>");

            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "DisallowInvalidOdfNamespaceExtensions" &&
                issue.XPath != null &&
                issue.XPath.Contains("@not-a-real-attribute"));
        }

        [Fact]
        public void ExtendedProfileReportsForeignExtensionIsolationWarning()
        {
            using MemoryStream ms = CreatePackage(
                "application/vnd.oasis.opendocument.text",
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:ext=\"https://example.invalid/ext\" office:version=\"1.4\"><office:body><office:text><ext:custom /></office:text></office:body></office:document-content>");

            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Extended);

            Assert.True(report.IsValid);
            Assert.Contains(report.Issues, issue => issue.RuleId == "RequireForeignExtensionIsolation");
        }

        [Fact]
        public void TaiwanProfileReportsMacroPackageEntries()
        {
            using MemoryStream ms = CreatePackage(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4"));

            using (OdfPackage package = OdfPackage.Open(ms))
            {
                package.WriteEntry("Basic/Standard/script.xlb", Encoding.UTF8.GetBytes("<library:library />"), "text/xml");
                OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.RocTaiwanGovernmentOdfTools);

                Assert.True(report.IsValid);
                Assert.Contains(report.Issues, issue => issue.RuleId == "DisallowMacroByDefault");
            }
        }

        [Fact]
        public void TaiwanProfileReportsExternalResourceReferences()
        {
            using MemoryStream ms = CreatePackage(
                "application/vnd.oasis.opendocument.text",
                "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" office:version=\"1.4\"><office:body><office:text><text:p><text:a xlink:type=\"simple\" xlink:href=\"https://example.invalid/image.png\">Link</text:a></text:p></office:text></office:body></office:document-content>");

            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.RocTaiwanGovernmentOdfTools);

            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.RuleId + ": " + i.Message)));
            Assert.Contains(report.Issues, issue => issue.RuleId == "RequireSafeExternalResourcePolicy");
        }

        [Fact]
        public void FlatValidatorDetectsFlatSpreadsheetFromMimeType()
        {
            using MemoryStream ms = CreateFlatDocument(
                "application/vnd.oasis.opendocument.spreadsheet",
                "1.4",
                "spreadsheet");

            OdfValidationReport report = OdfFlatDocumentValidator.Validate(
                ms,
                "workbook.fods",
                OdfComplianceProfiles.OasisOdf14Extended);

            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
            Assert.Equal(OdfDocumentKind.FlatSpreadsheet, report.DocumentKind);
            Assert.Equal(OdfVersion.Odf14, report.DetectedVersion);
        }

        [Fact]
        public void FlatValidatorReportsMismatchedExtension()
        {
            using MemoryStream ms = CreateFlatDocument(
                "application/vnd.oasis.opendocument.text",
                "1.4",
                "text");

            OdfValidationReport report = OdfFlatDocumentValidator.Validate(ms, "workbook.fods");

            Assert.True(report.IsValid);
            Assert.Equal(OdfDocumentKind.FlatText, report.DocumentKind);
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF2005");
        }

        [Fact]
        public void FlatValidatorReportsProfileDisallowedExtension()
        {
            using MemoryStream ms = CreateFlatDocument(
                "application/vnd.oasis.opendocument.text",
                "1.4",
                "text");

            OdfValidationReport report = OdfFlatDocumentValidator.Validate(
                ms,
                "document.xml",
                OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            Assert.Equal(OdfDocumentKind.FlatText, report.DocumentKind);
            Assert.Contains(report.Issues, issue =>
                issue.RuleId == "ODF1002" &&
                issue.ProfileId == OdfComplianceProfiles.OasisOdf14Strict.Id);
        }

        [Fact]
        public void FlatValidatorReportsMimeTypeAndBodyMismatch()
        {
            using MemoryStream ms = CreateFlatDocument(
                "application/vnd.oasis.opendocument.text",
                "1.4",
                "spreadsheet");

            OdfValidationReport report = OdfFlatDocumentValidator.Validate(ms, "document.fodt");

            Assert.False(report.IsValid);
            Assert.Equal(OdfDocumentKind.FlatText, report.DocumentKind);
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF2006");
        }

        [Fact]
        public void FlatValidatorRejectsUnknownOdfBodyElement()
        {
            using MemoryStream ms = CreateFlatDocument(
                "application/vnd.oasis.opendocument.text",
                "1.4",
                "not-a-document-kind");

            OdfValidationReport report = OdfFlatDocumentValidator.Validate(ms, "document.fodt", OdfComplianceProfiles.OasisOdf14Strict);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3002");
        }

        [Fact]
        public void FlatValidatorRejectsOdf14ForIso26300Profile()
        {
            using MemoryStream ms = CreateFlatDocument(
                "application/vnd.oasis.opendocument.text",
                "1.4",
                "text");

            OdfValidationReport report = OdfFlatDocumentValidator.Validate(ms, "document.fodt", OdfComplianceProfiles.IsoIec26300_2015);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF1001");
        }

        [Fact]
        public void FlatValidatorReportsMissingMimeType()
        {
            using MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(
                "<office:document xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\" />"));

            OdfValidationReport report = OdfFlatDocumentValidator.Validate(ms, "document.fodt");

            Assert.False(report.IsValid);
            Assert.Equal(OdfDocumentKind.FlatText, report.DocumentKind);
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF2002");
        }

        [Fact]
        public void FlatValidatorAppliesOptInSchemaPatternValidation()
        {
            OdfSchemaSet generated = CreateRootPatternSchema("document", "mimetype");
            OdfComplianceProfile profile = CreateSchemaPatternValidationProfile();

            using (OdfSchemaRegistry.RegisterSchema(generated, overwriteExisting: true))
            using (MemoryStream ms = CreateFlatDocument("application/vnd.oasis.opendocument.text", "1.4"))
            {
                OdfValidationReport validReport = OdfFlatDocumentValidator.Validate(ms, "document.fodt", profile);

                Assert.True(validReport.IsValid, string.Join(", ", validReport.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
            }

            using (OdfSchemaRegistry.RegisterSchema(generated, overwriteExisting: true))
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(
                "<office:document xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:body /></office:document>")))
            {
                OdfValidationReport invalidReport = OdfFlatDocumentValidator.Validate(ms, "document.fodt", profile);

                Assert.False(invalidReport.IsValid);
                Assert.Contains(invalidReport.Issues, issue =>
                    issue.RuleId == "ODF3101" &&
                    issue.ProfileId == profile.Id &&
                    issue.PackagePath == "document.fodt");
            }
        }

        [Fact]
        public void FlatValidatorUsesGeneratedStartPatternForSchemaValidation()
        {
            OdfSchemaSet generated = CreateStartPatternSchema("document", "mimetype");
            OdfComplianceProfile profile = CreateSchemaPatternValidationProfile();

            using (OdfSchemaRegistry.RegisterSchema(generated, overwriteExisting: true))
            using (MemoryStream ms = CreateFlatDocument("application/vnd.oasis.opendocument.text", "1.4"))
            {
                OdfValidationReport validReport = OdfFlatDocumentValidator.Validate(ms, "document.fodt", profile);

                Assert.True(validReport.IsValid, string.Join(", ", validReport.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
            }

            using (OdfSchemaRegistry.RegisterSchema(generated, overwriteExisting: true))
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(
                "<office:document xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:body /></office:document>")))
            {
                OdfValidationReport invalidReport = OdfFlatDocumentValidator.Validate(ms, "document.fodt", profile);

                Assert.False(invalidReport.IsValid);
                Assert.Contains(invalidReport.Issues, issue =>
                    issue.RuleId == "ODF3101" &&
                    issue.ProfileId == profile.Id &&
                    issue.PackagePath == "document.fodt");
            }
        }

        [Theory]
        [InlineData(OdfDocumentKind.Text)]
        [InlineData(OdfDocumentKind.TextTemplate)]
        [InlineData(OdfDocumentKind.TextMaster)]
        [InlineData(OdfDocumentKind.Spreadsheet)]
        [InlineData(OdfDocumentKind.SpreadsheetTemplate)]
        [InlineData(OdfDocumentKind.Presentation)]
        [InlineData(OdfDocumentKind.PresentationTemplate)]
        [InlineData(OdfDocumentKind.Graphics)]
        [InlineData(OdfDocumentKind.GraphicsTemplate)]
        [InlineData(OdfDocumentKind.Chart)]
        [InlineData(OdfDocumentKind.Formula)]
        [InlineData(OdfDocumentKind.Image)]
        [InlineData(OdfDocumentKind.Database)]
        public void FactoryCreatesMinimalPackageForSupportedKinds(OdfDocumentKind kind)
        {
            using var ms = new MemoryStream();
            using (OdfPackage package = OdfDocumentFactory.CreatePackage(ms, kind, leaveOpen: true))
            {
                package.Save();
            }

            ms.Position = 0;
            using OdfPackage reopened = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(reopened, OdfComplianceProfiles.OasisOdf14Extended);

            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
            Assert.Equal(kind, report.DocumentKind);
            Assert.Equal(OdfVersion.Odf14, report.DetectedVersion);
        }

        [Theory]
        [InlineData(OdfDocumentKind.Text, typeof(TextDocument))]
        [InlineData(OdfDocumentKind.TextTemplate, typeof(TextTemplateDocument))]
        [InlineData(OdfDocumentKind.TextMaster, typeof(TextMasterDocument))]
        [InlineData(OdfDocumentKind.Spreadsheet, typeof(SpreadsheetDocument))]
        [InlineData(OdfDocumentKind.SpreadsheetTemplate, typeof(SpreadsheetTemplateDocument))]
        [InlineData(OdfDocumentKind.Presentation, typeof(PresentationDocument))]
        [InlineData(OdfDocumentKind.PresentationTemplate, typeof(PresentationTemplateDocument))]
        [InlineData(OdfDocumentKind.Graphics, typeof(DrawingDocument))]
        [InlineData(OdfDocumentKind.GraphicsTemplate, typeof(GraphicsTemplateDocument))]
        public void HighLevelFactoryCreatesTypedDocuments(OdfDocumentKind kind, Type expectedType)
        {
            using OdfDocument document = OdfDocumentFactory.CreateDocument(kind);

            Assert.IsType(expectedType, document);

            using var ms = new MemoryStream();
            document.SaveToStream(ms);
            ms.Position = 0;

            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Extended);

            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
            Assert.Equal(kind, report.DocumentKind);
        }

        [Theory]
        [InlineData(OdfDocumentKind.Chart, "ChartDocument")]
        [InlineData(OdfDocumentKind.Formula, "FormulaDocument")]
        [InlineData(OdfDocumentKind.Image, nameof(OdfImageDocument))]
        [InlineData(OdfDocumentKind.Database, nameof(OdfDatabaseDocument))]
        public void HighLevelFactoryCreatesPackageLevelDocuments(OdfDocumentKind kind, string expectedTypeName)
        {
            using OdfDocument document = OdfDocument.Create(kind);
            Assert.Equal(expectedTypeName, document.GetType().Name);

            using var ms = new MemoryStream();
            document.SaveToStream(ms);
            ms.Position = 0;

            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Extended);

            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
            Assert.Equal(kind, report.DocumentKind);
        }

        [Fact]
        public void HighLevelFactoryLoadsDocumentFromStreamAndSavesToPath()
        {
            using var source = new MemoryStream();
            using (OdfPackage package = OdfDocumentFactory.CreatePackage(source, OdfDocumentKind.Spreadsheet, leaveOpen: true))
            {
                package.Save();
            }

            source.Position = 0;
            using OdfDocument document = OdfDocument.Load(source, "sheet.ods");
            Assert.IsType<SpreadsheetDocument>(document);

            string path = Path.Combine(Path.GetTempPath(), "OdfKit.FactoryLoadSave." + Guid.NewGuid().ToString("N") + ".ods");
            try
            {
                document.Save(path);
                using OdfDocument loaded = OdfDocumentFactory.LoadDocument(path);
                Assert.IsType<SpreadsheetDocument>(loaded);
                Assert.Equal("application/vnd.oasis.opendocument.spreadsheet", loaded.Package.MimeType);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void PackageSaveWritesMimetypeEntryFirstAndStored()
        {
            using MemoryStream ms = CreatePackage(
                "application/vnd.oasis.opendocument.text",
                CreateDocumentContent("1.4"));

            using (var archive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true))
            {
                ZipArchiveEntry firstEntry = archive.Entries[0];

                Assert.Equal("mimetype", firstEntry.FullName);
                Assert.Equal(firstEntry.Length, firstEntry.CompressedLength);
            }

            ms.Position = 0;
            using OdfPackage package = OdfPackage.Open(ms);
            OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
            Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF0003" || issue.RuleId == "ODF0004");
        }

        [Theory]
        [InlineData(OdfDocumentKind.FlatText, "document.fodt")]
        [InlineData(OdfDocumentKind.FlatSpreadsheet, "sheet.fods")]
        [InlineData(OdfDocumentKind.FlatPresentation, "deck.fodp")]
        [InlineData(OdfDocumentKind.FlatGraphics, "drawing.fodg")]
        public void FactoryWritesMinimalFlatXmlForSupportedKinds(OdfDocumentKind kind, string fileName)
        {
            using var ms = new MemoryStream();
            OdfDocumentFactory.WriteFlatXml(ms, kind);

            ms.Position = 0;
            OdfValidationReport report = OdfFlatDocumentValidator.Validate(ms, fileName, OdfComplianceProfiles.OasisOdf14Extended);

            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
            Assert.Equal(kind, report.DocumentKind);
            Assert.Equal(OdfVersion.Odf14, report.DetectedVersion);
        }

        [Fact]
        public void FactoryRejectsFlatKindForPackagedCreate()
        {
            using var ms = new MemoryStream();

            Assert.Throws<System.ArgumentException>(() =>
                OdfDocumentFactory.CreatePackage(ms, OdfDocumentKind.FlatText, leaveOpen: true));
        }

        [Fact]
        public void FactoryRejectsTemplateKindForFlatXml()
        {
            using var ms = new MemoryStream();

            Assert.Throws<System.ArgumentException>(() =>
                OdfDocumentFactory.WriteFlatXml(ms, OdfDocumentKind.TextTemplate));
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
                foreach (KeyValuePair<string, string> entry in additionalXmlEntries ?? Array.Empty<KeyValuePair<string, string>>())
                {
                    package.WriteEntry(entry.Key, Encoding.UTF8.GetBytes(entry.Value), "text/xml");
                }

                package.Save();
            }

            ms.Position = 0;
            return ms;
        }

        private static MemoryStream CreateZipWithoutManifest()
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
                    writer.Write(CreateDocumentContent("1.4"));
                }
            }

            ms.Position = 0;
            return ms;
        }

        private static MemoryStream CreateZipWithMimetypeLayout(
            string mimeType,
            string contentXml,
            bool mimetypeFirst,
            CompressionLevel mimetypeCompression)
        {
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                if (mimetypeFirst)
                {
                    WriteZipTextEntry(zip, "mimetype", mimeType, mimetypeCompression);
                }

                WriteZipTextEntry(zip, "content.xml", contentXml, CompressionLevel.Fastest);

                if (!mimetypeFirst)
                {
                    WriteZipTextEntry(zip, "mimetype", mimeType, mimetypeCompression);
                }

                WriteZipTextEntry(
                    zip,
                    "META-INF/manifest.xml",
                    "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.4\">" +
                    "<manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"" + mimeType + "\" />" +
                    "<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\" />" +
                    "</manifest:manifest>",
                    CompressionLevel.Fastest);
            }

            ms.Position = 0;
            return ms;
        }

        private static MemoryStream CreateZipWithDuplicateContentEntry(string mimeType, string contentXml)
        {
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteZipTextEntry(zip, "mimetype", mimeType, CompressionLevel.NoCompression);
                WriteZipTextEntry(zip, "content.xml", contentXml, CompressionLevel.Fastest);
                WriteZipTextEntry(zip, "content.xml", contentXml, CompressionLevel.Fastest);
                WriteZipTextEntry(
                    zip,
                    "META-INF/manifest.xml",
                    "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.4\">" +
                    "<manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"" + mimeType + "\" />" +
                    "<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\" />" +
                    "</manifest:manifest>",
                    CompressionLevel.Fastest);
            }

            ms.Position = 0;
            return ms;
        }

        private static MemoryStream CreateZipWithManifestRootMediaType(
            string mimeType,
            string manifestRootMediaType,
            string contentXml)
        {
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                ZipArchiveEntry mimetype = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
                using (Stream stream = mimetype.Open())
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.Write(mimeType);
                }

                ZipArchiveEntry content = zip.CreateEntry("content.xml", CompressionLevel.Fastest);
                using (Stream stream = content.Open())
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.Write(contentXml);
                }

                ZipArchiveEntry manifest = zip.CreateEntry("META-INF/manifest.xml", CompressionLevel.Fastest);
                using (Stream stream = manifest.Open())
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.Write(
                        "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.4\">" +
                        "<manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"" + manifestRootMediaType + "\" />" +
                        "<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\" />" +
                        "</manifest:manifest>");
                }
            }

            ms.Position = 0;
            return ms;
        }

        private static MemoryStream CreateZipWithRawManifest(string mimeType, string contentXml, string manifestXml)
        {
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteZipTextEntry(zip, "mimetype", mimeType, CompressionLevel.NoCompression);
                WriteZipTextEntry(zip, "content.xml", contentXml, CompressionLevel.Fastest);
                WriteZipTextEntry(zip, "META-INF/manifest.xml", manifestXml, CompressionLevel.Fastest);
            }

            ms.Position = 0;
            return ms;
        }

        private static MemoryStream CreateZipWithCustomManifestEntries(
            string mimeType,
            string contentXml,
            IEnumerable<KeyValuePair<string, string>> manifestEntries,
            string contentMediaType = "text/xml")
        {
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                ZipArchiveEntry mimetype = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
                using (Stream stream = mimetype.Open())
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.Write(mimeType);
                }

                ZipArchiveEntry content = zip.CreateEntry("content.xml", CompressionLevel.Fastest);
                using (Stream stream = content.Open())
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.Write(contentXml);
                }

                ZipArchiveEntry manifest = zip.CreateEntry("META-INF/manifest.xml", CompressionLevel.Fastest);
                using (Stream stream = manifest.Open())
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.Write(
                        "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.4\">" +
                        "<manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"" + mimeType + "\" />" +
                        "<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"" + contentMediaType + "\" />");
                    foreach (KeyValuePair<string, string> entry in manifestEntries)
                    {
                        writer.Write(
                            "<manifest:file-entry manifest:full-path=\"" + entry.Key + "\" manifest:media-type=\"" + entry.Value + "\" />");
                    }

                    writer.Write("</manifest:manifest>");
                }
            }

            ms.Position = 0;
            return ms;
        }

        private static void WriteZipTextEntry(ZipArchive zip, string name, string text, CompressionLevel compressionLevel)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name, compressionLevel);
            using Stream stream = entry.Open();
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.Write(text);
        }

        private static string CreateDocumentContent(string version, string bodyElement = "text")
        {
            return "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"" +
                version +
                "\"><office:body><office:" +
                bodyElement +
                " /></office:body></office:document-content>";
        }

        private static MemoryStream CreateFlatDocument(string mimeType, string version, string bodyElement = "body")
        {
            string xml =
                "<office:document xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:mimetype=\"" +
                mimeType +
                "\" office:version=\"" +
                version +
                "\"><office:body>";

            if (bodyElement != "body")
            {
                xml += "<office:" + bodyElement + " />";
            }

            xml += "</office:body></office:document>";

            return new MemoryStream(Encoding.UTF8.GetBytes(xml));
        }

        private static OdfComplianceProfile CreateSchemaPatternValidationProfile()
        {
            return new OdfComplianceProfile(
                "Test_SchemaPatternValidation",
                "Test",
                "Test",
                null,
                null,
                OdfPolicyAuthorityLevel.Compatibility,
                OdfProfileVerificationStatus.CompatibilityOnly,
                OdfVersionRange.Exact(OdfVersion.Odf14),
                new[] { ".odt", ".fodt" },
                new[] { "application/vnd.oasis.opendocument.text" },
                new[]
                {
                    new OdfPolicyRule(
                        "RequireSchemaPatternValidation",
                        "Validate the XML root element against generated RELAX NG pattern metadata.",
                        OdfIssueSeverity.Error)
                });
        }

        private static OdfSchemaSet CreateRootPatternSchema(string rootLocalName, string requiredAttributeLocalName)
        {
            return new OdfSchemaSet(
                OdfVersion.Odf14,
                new Uri("https://example.invalid/root-pattern.rng"),
                "generated",
                Array.Empty<OdfElementDefinition>(),
                patterns: new[]
                {
                    new OdfSchemaPatternDefinition(
                        rootLocalName,
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Office,
                                rootLocalName,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: CreateRootChildren(rootLocalName, requiredAttributeLocalName))
                        })
                });
        }

        private static OdfSchemaSet CreateStartPatternSchema(string rootLocalName, string requiredAttributeLocalName)
        {
            return new OdfSchemaSet(
                OdfVersion.Odf14,
                new Uri("https://example.invalid/start-pattern.rng"),
                "generated",
                Array.Empty<OdfElementDefinition>(),
                patterns: new[]
                {
                    new OdfSchemaPatternDefinition(
                        "start",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Ref,
                                "exactlyOne",
                                string.Empty,
                                string.Empty,
                                "generated-entry-point",
                                string.Empty,
                                string.Empty)
                        }),
                    new OdfSchemaPatternDefinition(
                        "generated-entry-point",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Office,
                                rootLocalName,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: CreateRootChildren(rootLocalName, requiredAttributeLocalName))
                        })
                });
        }

        private static OdfSchemaSet CreatePackageEntryFallbackSchema()
        {
            return new OdfSchemaSet(
                OdfVersion.Odf14,
                new Uri("https://example.invalid/package-entry-patterns.rng"),
                "generated",
                Array.Empty<OdfElementDefinition>(),
                patterns: new[]
                {
                    new OdfSchemaPatternDefinition(
                        "start",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Office,
                                "document-content",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: CreateRootChildren("document-content", "mimetype"))
                        }),
                    new OdfSchemaPatternDefinition(
                        "document-styles",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Office,
                                "document-styles",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Attribute,
                                        "exactlyOne",
                                        OdfNamespaces.Office,
                                        "version",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty)
                                })
                        })
                });
        }

        private static OdfSchemaPatternNode[] CreateRootAttributePatterns(string requiredAttributeLocalName)
        {
            if (string.Equals(requiredAttributeLocalName, "version", StringComparison.Ordinal))
            {
                return new[] { CreateOfficeAttributePattern("version") };
            }

            return new[]
            {
                CreateOfficeAttributePattern("version"),
                CreateOfficeAttributePattern(requiredAttributeLocalName)
            };
        }

        private static OdfSchemaPatternNode CreateOfficeAttributePattern(string localName)
        {
            return new OdfSchemaPatternNode(
                OdfSchemaPatternNodeKind.Attribute,
                "exactlyOne",
                OdfNamespaces.Office,
                localName,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private static OdfSchemaPatternNode[] CreateRootChildren(string rootLocalName, string requiredAttributeLocalName)
        {
            List<OdfSchemaPatternNode> children = CreateRootAttributePatterns(requiredAttributeLocalName).ToList();
            if (string.Equals(rootLocalName, "document", StringComparison.Ordinal) ||
                string.Equals(rootLocalName, "document-content", StringComparison.Ordinal))
            {
                children.Add(CreateOfficeBodyPattern());
            }

            return children.ToArray();
        }

        private static OdfSchemaPatternNode CreateOfficeBodyPattern()
        {
            return new OdfSchemaPatternNode(
                OdfSchemaPatternNodeKind.Element,
                "exactlyOne",
                OdfNamespaces.Office,
                "body",
                string.Empty,
                string.Empty,
                string.Empty,
                children: new[]
                {
                    new OdfSchemaPatternNode(
                        OdfSchemaPatternNodeKind.ZeroOrMore,
                        "zeroOrMore",
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        children: new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.AnyName,
                                "exactlyOne",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                nameClasses: new[]
                                {
                                    new OdfSchemaNameClass(
                                        OdfSchemaNameClassKind.NamespaceName,
                                        OdfNamespaces.Office,
                                        string.Empty,
                                        isExcept: false)
                                })
                        })
                });
        }

        private static OdfSchemaSet CreatePatternValidationSchema()
        {
            return new OdfSchemaSet(
                OdfVersion.Odf14,
                new Uri("https://example.invalid/generated.rng"),
                "generated",
                Array.Empty<OdfElementDefinition>(),
                patterns: new[]
                {
                    new OdfSchemaPatternDefinition(
                        "root",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Office,
                                "document-content",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Attribute,
                                        "exactlyOne",
                                        OdfNamespaces.Office,
                                        "version",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty),
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.ZeroOrMore,
                                        "zeroOrMore",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Ref,
                                                "exactlyOne",
                                                string.Empty,
                                                string.Empty,
                                                "paragraph",
                                                string.Empty,
                                                string.Empty)
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "paragraph",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "p",
                                string.Empty,
                                string.Empty,
                                string.Empty)
                        }),
                    new OdfSchemaPatternDefinition(
                        "body-kind",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Choice,
                                "exactlyOne",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Element,
                                        "exactlyOne",
                                        OdfNamespaces.Office,
                                        "text",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty),
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Element,
                                        "exactlyOne",
                                        OdfNamespaces.Office,
                                        "spreadsheet",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "foreign-or-odf",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.AnyName,
                                "exactlyOne",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                new[]
                                {
                                    new OdfSchemaNameClass(
                                        OdfSchemaNameClassKind.AnyName,
                                        string.Empty,
                                        string.Empty,
                                        isExcept: false),
                                    new OdfSchemaNameClass(
                                        OdfSchemaNameClassKind.NamespaceName,
                                        OdfNamespaces.Draw,
                                        string.Empty,
                                        isExcept: true)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "wildcard-empty-element",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.AnyName,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        new[]
                                        {
                                            new OdfSchemaNameClass(
                                                OdfSchemaNameClassKind.AnyName,
                                                string.Empty,
                                                string.Empty,
                                                isExcept: false)
                                        },
                                        new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Except,
                                                "exactlyOne",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                children: new[]
                                                {
                                                    new OdfSchemaPatternNode(
                                                        OdfSchemaPatternNodeKind.NamespaceName,
                                                        "exactlyOne",
                                                        string.Empty,
                                                        string.Empty,
                                                        string.Empty,
                                                        string.Empty,
                                                        string.Empty,
                                                        new[]
                                                        {
                                                            new OdfSchemaNameClass(
                                                                OdfSchemaNameClassKind.NamespaceName,
                                                                OdfNamespaces.Draw,
                                                                string.Empty,
                                                                isExcept: true)
                                                        })
                                                })
                                        }),
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Empty,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "other-wrapped-root",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Other,
                                "exactlyOne",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Element,
                                        "exactlyOne",
                                        OdfNamespaces.Text,
                                        "p",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "other-wrapped-content",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Office,
                                "document-content",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Other,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Element,
                                                "exactlyOne",
                                                OdfNamespaces.Text,
                                                "p",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty)
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "empty-then-paragraph",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Office,
                                "document-content",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Group,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Empty,
                                                "exactlyOne",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty),
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Element,
                                                "exactlyOne",
                                                OdfNamespaces.Text,
                                                "p",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty)
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "interleaved-content",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Office,
                                "document-content",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Interleave,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Element,
                                                "exactlyOne",
                                                OdfNamespaces.Text,
                                                "p",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty),
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Element,
                                                "exactlyOne",
                                                OdfNamespaces.Table,
                                                "table",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty)
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "interleaved-repeated-content",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Office,
                                "document-content",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Interleave,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.ZeroOrMore,
                                                "zeroOrMore",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                children: new[]
                                                {
                                                    new OdfSchemaPatternNode(
                                                        OdfSchemaPatternNodeKind.Element,
                                                        "exactlyOne",
                                                        OdfNamespaces.Text,
                                                        "p",
                                                        string.Empty,
                                                        string.Empty,
                                                        string.Empty)
                                                }),
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Element,
                                                "exactlyOne",
                                                OdfNamespaces.Table,
                                                "table",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty)
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "ref-sequence-content",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Office,
                                "document-content",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Ref,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        "paragraph-then-table",
                                        string.Empty,
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "paragraph-then-table",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Group,
                                "exactlyOne",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Element,
                                        "exactlyOne",
                                        OdfNamespaces.Text,
                                        "p",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty),
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Element,
                                        "exactlyOne",
                                        OdfNamespaces.Table,
                                        "table",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "start-group-wrapped-root",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Group,
                                "exactlyOne",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Other,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty),
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Ref,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        "body-kind",
                                        string.Empty,
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "integer-text",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "sequence-ref",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "integer",
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "token-text",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "sequence-ref",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "token",
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "custom-datatype-library-text",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "sequence-ref",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "integer",
                                        string.Empty,
                                        dataTypeLibrary: "https://example.invalid/custom-datatypes")
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "unknown-datatype-text",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "sequence-ref",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "customInteger",
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "literal-value",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "span",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Value,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "token",
                                        "approved")
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "string-literal-value",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "span",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Value,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "string",
                                        "two  words")
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "decimal-literal-value",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "sequence-ref",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Value,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "decimal",
                                        "1.0")
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "typed-attribute",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Table,
                                "table",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Attribute,
                                        "exactlyOne",
                                        OdfNamespaces.Table,
                                        "number-rows-repeated",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Data,
                                                "exactlyOne",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                "positiveInteger",
                                                string.Empty)
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "other-wrapped-typed-attribute",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Table,
                                "table",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Other,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Attribute,
                                                "exactlyOne",
                                                OdfNamespaces.Table,
                                                "number-rows-repeated",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                children: new[]
                                                {
                                                    new OdfSchemaPatternNode(
                                                        OdfSchemaPatternNodeKind.Data,
                                                        "exactlyOne",
                                                        string.Empty,
                                                        string.Empty,
                                                        string.Empty,
                                                        "positiveInteger",
                                                        string.Empty)
                                                })
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "attribute-ref-host",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Table,
                                "table",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Ref,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        "positive-row-repeat-attribute",
                                        string.Empty,
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "positive-row-repeat-attribute",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Attribute,
                                "exactlyOne",
                                OdfNamespaces.Table,
                                "number-rows-repeated",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "positiveInteger",
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "mixed-paragraph",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "p",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Mixed,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.ZeroOrMore,
                                                "zeroOrMore",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                children: new[]
                                                {
                                                    new OdfSchemaPatternNode(
                                                        OdfSchemaPatternNodeKind.Element,
                                                        "exactlyOne",
                                                        OdfNamespaces.Text,
                                                        "span",
                                                        string.Empty,
                                                        string.Empty,
                                                        string.Empty)
                                                })
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "text-or-span-choice",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "p",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Choice,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Text,
                                                "exactlyOne",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty),
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Element,
                                                "exactlyOne",
                                                OdfNamespaces.Text,
                                                "span",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty)
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "integer-list",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "span",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.List,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.OneOrMore,
                                                "oneOrMore",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                children: new[]
                                                {
                                                    new OdfSchemaPatternNode(
                                                        OdfSchemaPatternNodeKind.Data,
                                                        "exactlyOne",
                                                        string.Empty,
                                                        string.Empty,
                                                        string.Empty,
                                                        "integer",
                                                        string.Empty)
                                                })
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "bounded-integer-text",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "sequence-ref",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "integer",
                                        string.Empty,
                                        dataParameters: new[]
                                        {
                                            new KeyValuePair<string, string>("minInclusive", "1"),
                                            new KeyValuePair<string, string>("maxInclusive", "10")
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "bounded-decimal-text",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "sequence-ref",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "decimal",
                                        string.Empty,
                                        dataParameters: new[]
                                        {
                                            new KeyValuePair<string, string>(
                                                "minInclusive",
                                                "123456789012345678901234567890.20"),
                                            new KeyValuePair<string, string>(
                                                "maxInclusive",
                                                "123456789012345678901234567890.30")
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "integer-text-except-seven",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "sequence-ref",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "integer",
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Except,
                                                "exactlyOne",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                children: new[]
                                                {
                                                    new OdfSchemaPatternNode(
                                                        OdfSchemaPatternNodeKind.Value,
                                                        "exactlyOne",
                                                        string.Empty,
                                                        string.Empty,
                                                        string.Empty,
                                                        "integer",
                                                        "7")
                                                })
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "positive-attribute-except-one",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Table,
                                "table",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Attribute,
                                        "exactlyOne",
                                        OdfNamespaces.Table,
                                        "number-rows-repeated",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Data,
                                                "exactlyOne",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                "positiveInteger",
                                                string.Empty,
                                                children: new[]
                                                {
                                                    new OdfSchemaPatternNode(
                                                        OdfSchemaPatternNodeKind.Except,
                                                        "exactlyOne",
                                                        string.Empty,
                                                        string.Empty,
                                                        string.Empty,
                                                        string.Empty,
                                                        string.Empty,
                                                        children: new[]
                                                        {
                                                            new OdfSchemaPatternNode(
                                                                OdfSchemaPatternNodeKind.Value,
                                                                "exactlyOne",
                                                                string.Empty,
                                                                string.Empty,
                                                                string.Empty,
                                                                "positiveInteger",
                                                                "1")
                                                        })
                                                })
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "integer-list-except-zero",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "span",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.List,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.OneOrMore,
                                                "oneOrMore",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                children: new[]
                                                {
                                                    new OdfSchemaPatternNode(
                                                        OdfSchemaPatternNodeKind.Data,
                                                        "exactlyOne",
                                                        string.Empty,
                                                        string.Empty,
                                                        string.Empty,
                                                        "integer",
                                                        string.Empty,
                                                        children: new[]
                                                        {
                                                            new OdfSchemaPatternNode(
                                                                OdfSchemaPatternNodeKind.Except,
                                                                "exactlyOne",
                                                                string.Empty,
                                                                string.Empty,
                                                                string.Empty,
                                                                string.Empty,
                                                                string.Empty,
                                                                children: new[]
                                                                {
                                                                    new OdfSchemaPatternNode(
                                                                        OdfSchemaPatternNodeKind.Value,
                                                                        "exactlyOne",
                                                                        string.Empty,
                                                                        string.Empty,
                                                                        string.Empty,
                                                                        "integer",
                                                                        "0")
                                                                })
                                                        })
                                                })
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "not-allowed-content",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "p",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.NotAllowed,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "not-allowed-attribute-value",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Table,
                                "table",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Attribute,
                                        "exactlyOne",
                                        OdfNamespaces.Table,
                                        "number-rows-repeated",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.NotAllowed,
                                                "exactlyOne",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty)
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "not-allowed-list-token",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "span",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.List,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.NotAllowed,
                                                "exactlyOne",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty)
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "duration-text",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "sequence-ref",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "duration",
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "time-text",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "sequence-ref",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "time",
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "uri-attribute",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Draw,
                                "image",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Attribute,
                                        "exactlyOne",
                                        OdfNamespaces.XLink,
                                        "href",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Data,
                                                "exactlyOne",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                "anyURI",
                                                string.Empty)
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "ncname-text",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "span",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "NCName",
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "qname-text",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "span",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "QName",
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "hex-binary-text",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "span",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "hexBinary",
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "base64-binary-text",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "span",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "base64Binary",
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "idrefs-text",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "span",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "IDREFS",
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "nmtokens-text",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "span",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "NMTOKENS",
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "nonnegative-integer-text",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "span",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "nonNegativeInteger",
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "normalized-string-text",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "span",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "normalizedString",
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "decimal-text",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Text,
                                "span",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Data,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        "decimal",
                                        string.Empty)
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "optional-typed-attribute",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Table,
                                "table",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Optional,
                                        "optional",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Attribute,
                                                "exactlyOne",
                                                OdfNamespaces.Table,
                                                "number-rows-repeated",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                children: new[]
                                                {
                                                    new OdfSchemaPatternNode(
                                                        OdfSchemaPatternNodeKind.Data,
                                                        "exactlyOne",
                                                        string.Empty,
                                                        string.Empty,
                                                        string.Empty,
                                                        "positiveInteger",
                                                        string.Empty)
                                                })
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "choice-attribute",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Table,
                                "table",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Choice,
                                        "choice",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Attribute,
                                                "exactlyOne",
                                                OdfNamespaces.Table,
                                                "number-rows-repeated",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                children: new[]
                                                {
                                                    new OdfSchemaPatternNode(
                                                        OdfSchemaPatternNodeKind.Data,
                                                        "exactlyOne",
                                                        string.Empty,
                                                        string.Empty,
                                                        string.Empty,
                                                        "positiveInteger",
                                                        string.Empty)
                                                }),
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Attribute,
                                                "exactlyOne",
                                                OdfNamespaces.Table,
                                                "name",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                children: new[]
                                                {
                                                    new OdfSchemaPatternNode(
                                                        OdfSchemaPatternNodeKind.Data,
                                                        "exactlyOne",
                                                        string.Empty,
                                                        string.Empty,
                                                        string.Empty,
                                                        "token",
                                                        string.Empty)
                                                })
                                        })
                                })
                        }),
                    new OdfSchemaPatternDefinition(
                        "wildcard-positive-integer-attribute",
                        new[]
                        {
                            new OdfSchemaPatternNode(
                                OdfSchemaPatternNodeKind.Element,
                                "exactlyOne",
                                OdfNamespaces.Table,
                                "table",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                children: new[]
                                {
                                    new OdfSchemaPatternNode(
                                        OdfSchemaPatternNodeKind.Attribute,
                                        "exactlyOne",
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        string.Empty,
                                        children: new[]
                                        {
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.AnyName,
                                                "exactlyOne",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                new[]
                                                {
                                                    new OdfSchemaNameClass(
                                                        OdfSchemaNameClassKind.AnyName,
                                                        string.Empty,
                                                        string.Empty,
                                                        isExcept: false)
                                                }),
                                            new OdfSchemaPatternNode(
                                                OdfSchemaPatternNodeKind.Data,
                                                "exactlyOne",
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                "positiveInteger",
                                                string.Empty)
                                        })
                                })
                        })
                });
        }

    }
}
