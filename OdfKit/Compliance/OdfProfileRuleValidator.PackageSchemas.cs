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
        OdfVersion version,
        List<OdfValidationIssue> issues)
    {
        OdfPolicyRule? schemaRule = FindRule(profile, "RequirePackageSchemaValidation");
        if (schemaRule is null)
        {
            return;
        }

        // 1.0／1.1 的 manifest 沒有 version，須以文件版本選 schema；
        // 有宣告時仍保留 package 與 document 各自版本的既有契約。
        OdfVersion metadataVersion = package.ManifestRootInfo?.Version is null ? version : package.Version;

        ValidatePackageMetadataEntry(
            package,
            ManifestPath,
            OdfPackageSchemaRegistry.GetManifestSchema(metadataVersion),
            "start",
            schemaRule,
            profile,
            issues);

        OdfSchemaSet? signatureSchema = OdfPackageSchemaRegistry.GetDigitalSignatureSchema(metadataVersion);
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

        return global::OdfKit.Internal.OdfStringHelper.Contains(entryName, "signatures", StringComparison.Ordinal);
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
            XDocument document = XDocument.Load(new OdfKit.Core.OdfDepthLimitedXmlReader(reader), LoadOptions.None);
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

            if (schema.Version >= OdfVersion.Odf12)
            {
                var xmlIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (XElement element in document.Root.DescendantsAndSelf())
                {
                    string? xmlId = (string?)element.Attribute(XNamespace.Xml + "id");
                    if (xmlId is not null)
                        ValidateXmlId(xmlId, xmlIds, entryName, "/" + element.Name.LocalName + "/@xml:id", rule, profile, issues);
                }
            }

            OdfSchemaPatternValidationResult result = OdfSchemaPatternValidator.ValidateElement(
                document.Root,
                schema,
                patternName);
            if (IsDigitalSignatureEntry(entryName))
                ValidateDigitalSignatureSemantics(document.Root, entryName, rule, profile, issues);
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

    private static void ValidateDigitalSignatureSemantics(
        XElement root,
        string entryName,
        OdfPolicyRule rule,
        OdfComplianceProfile profile,
        List<OdfValidationIssue> issues)
    {
        XNamespace signatureNamespace = "http://www.w3.org/2000/09/xmldsig#";
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (XElement signature in root.Elements(signatureNamespace + "Signature"))
        {
            string? identifier = (string?)signature.Attribute("Id");
            if (string.IsNullOrWhiteSpace(identifier) || !identifiers.Add(identifier!))
            {
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3114",
                    "Each ODF digital signature must have a nonempty unique Id attribute.",
                    entryName,
                    "/document-signatures/Signature/@Id",
                    profileId: profile.Id));
            }

            if (signature.Elements(signatureNamespace + "KeyInfo").Count() != 1)
            {
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3115",
                    "Each ODF digital signature must contain exactly one ds:KeyInfo element.",
                    entryName,
                    "/document-signatures/Signature/KeyInfo",
                    profileId: profile.Id));
            }

            foreach (XElement reference in signature.Elements(signatureNamespace + "SignedInfo")
                         .Elements(signatureNamespace + "Reference"))
            {
                string? uri = (string?)reference.Attribute("URI");
                if (string.IsNullOrEmpty(uri) || uri![0] == '#' || Uri.TryCreate(uri, UriKind.Absolute, out _))
                    continue;

                foreach (XElement transform in reference.Elements(signatureNamespace + "Transforms")
                             .Elements(signatureNamespace + "Transform"))
                {
                    string? algorithm = (string?)transform.Attribute("Algorithm");
                    if (algorithm is "http://www.w3.org/TR/2001/REC-xml-c14n-20010315"
                        or "http://www.w3.org/TR/2001/REC-xml-c14n-20010315#WithComments"
                        or "http://www.w3.org/2006/12/xml-c14n11"
                        or "http://www.w3.org/2006/12/xml-c14n11#WithComments"
                        or "http://www.w3.org/2001/10/xml-exc-c14n#"
                        or "http://www.w3.org/2001/10/xml-exc-c14n#WithComments")
                        continue;

                    issues.Add(new OdfValidationIssue(
                        rule.DefaultSeverity,
                        "ODF3116",
                        "ODF signature references to package files may only use canonicalization transforms.",
                        entryName,
                        "/document-signatures/Signature/SignedInfo/Reference/Transforms/Transform",
                        profileId: profile.Id));
                }
            }
        }
    }
}
