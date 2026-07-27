using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Xml;
using System.Xml.Linq;
using OdfKit.Core;

namespace OdfKit.Compliance;

internal static partial class OdfProfileRuleValidator
{
    private const string ManifestPath = "META-INF/manifest.xml";

    private static void ValidatePackageMetadataSchemas(
        OdfPackage package,
        OdfComplianceProfile profile,
        List<OdfValidationIssue> issues)
    {
        OdfPolicyRule? schemaRule = FindRule(profile, "RequirePackageSchemaValidation");
        if (schemaRule is null)
        {
            return;
        }

        ValidatePackageMetadataEntry(
            package,
            ManifestPath,
            OdfPackageSchemaRegistry.GetManifestSchema(package.Version),
            "start",
            schemaRule,
            profile,
            issues);

        OdfSchemaSet? signatureSchema = OdfPackageSchemaRegistry.GetDigitalSignatureSchema(package.Version);
        foreach (string entryName in package.Entries.Keys)
        {
            if (!IsDigitalSignatureEntry(entryName))
            {
                continue;
            }

            if (signatureSchema is null)
            {
                issues.Add(new OdfValidationIssue(
                    schemaRule.DefaultSeverity,
                    "ODF3112",
                    "This ODF version does not define a package digital-signature schema.",
                    entryName,
                    profileId: profile.Id));
                continue;
            }

            ValidatePackageMetadataEntry(
                package,
                entryName,
                signatureSchema,
                "start",
                schemaRule,
                profile,
                issues);
        }
    }

    private static void ValidateStrictPackageMetadataEntries(
        OdfPackage package,
        OdfComplianceProfile profile,
        List<OdfValidationIssue> issues)
    {
        OdfPolicyRule? rule = FindRule(profile, "RequireStrictPackageConformance");
        if (rule is null)
        {
            return;
        }

        foreach (string entryName in package.Entries.Keys)
        {
            if (!entryName.StartsWith("META-INF/", StringComparison.Ordinal) ||
                entryName == ManifestPath ||
                IsDigitalSignatureEntry(entryName))
            {
                continue;
            }

            issues.Add(new OdfValidationIssue(
                rule.DefaultSeverity,
                "ODF3113",
                "A conforming non-extended ODF package cannot contain this META-INF entry.",
                entryName,
                profileId: profile.Id));
        }
    }

    private static bool IsDigitalSignatureEntry(string entryName)
    {
        if (!entryName.StartsWith("META-INF/", StringComparison.Ordinal))
        {
            return false;
        }

        string fileName = entryName.Substring("META-INF/".Length);
        return fileName.IndexOf('/') < 0 &&
            global::OdfKit.Internal.OdfStringHelper.Contains(fileName, "signatures", StringComparison.Ordinal);
    }

    private static void ValidatePackageMetadataEntry(
        OdfPackage package,
        string entryName,
        OdfSchemaSet schema,
        string patternName,
        OdfPolicyRule rule,
        OdfComplianceProfile profile,
        List<OdfValidationIssue> issues)
    {
        if (!package.HasEntry(entryName))
        {
            return;
        }

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreWhitespace = true,
                MaxCharactersFromEntities = 0,
                MaxCharactersInDocument = package.LoadOptions.MaxXmlCharactersInDocument,
                CloseInput = true
            };

            using Stream stream = package.GetEntryStream(entryName);
            using XmlReader reader = XmlReader.Create(stream, settings);
            XDocument document = XDocument.Load(reader, LoadOptions.None);
            if (document.Root is null)
            {
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3110",
                    "Package metadata XML does not contain a root element.",
                    entryName,
                    profileId: profile.Id));
                return;
            }

            OdfSchemaPatternValidationResult result = OdfSchemaPatternValidator.ValidateElement(
                document.Root,
                schema,
                patternName);
            if (result.IsMatch)
            {
                return;
            }

            foreach (OdfValidationIssue issue in result.Issues)
            {
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3111",
                    issue.Message,
                    entryName,
                    "/" + document.Root.Name.LocalName,
                    profileId: profile.Id));
            }
        }
        catch (XmlException ex)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Fatal,
                "ODF0301",
                $"Package metadata XML is not well-formed: {ex.Message}",
                entryName,
                profileId: profile.Id));
        }
        catch (IOException ex)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Fatal,
                "ODF0302",
                $"Package metadata XML cannot be read: {ex.Message}",
                entryName,
                profileId: profile.Id));
        }
        catch (SecurityException ex)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Fatal,
                "ODF0303",
                $"Package metadata XML failed security validation: {ex.Message}",
                entryName,
                profileId: profile.Id));
        }
    }
}
