using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using OdfKit.Core;

namespace OdfKit.Compliance;

internal static partial class OdfProfileRuleValidator
{
    #region Namespace & Version Rules

    private static OdfVersion? FindMinimumElementVersion(string namespaceUri, string localName)
    {
        OdfVersion[] versions =
        [
            OdfVersion.Odf10,
            OdfVersion.Odf11,
            OdfVersion.Odf12,
            OdfVersion.Odf13,
            OdfVersion.Odf14
        ];
        foreach (var version in versions)
        {
            var schema = OdfSchemaRegistry.GetSchema(version);
            if (schema is not null && schema.ContainsElement(namespaceUri, localName))
            {
                return version;
            }
        }
        return null;
    }

    private static OdfVersion? FindMinimumAttributeVersion(string namespaceUri, string localName)
    {
        OdfVersion[] versions =
        [
            OdfVersion.Odf10,
            OdfVersion.Odf11,
            OdfVersion.Odf12,
            OdfVersion.Odf13,
            OdfVersion.Odf14
        ];
        foreach (var version in versions)
        {
            var schema = OdfSchemaRegistry.GetSchema(version);
            if (schema is not null && schema.FindAttribute(namespaceUri, localName) is not null)
            {
                return version;
            }
        }
        return null;
    }

    private static void ValidateOdfNamespaceElement(
        XmlReader reader,
        string? packagePath,
        string xPath,
        OdfComplianceProfile profile,
        OdfSchemaSet schema,
        List<OdfValidationIssue> issues)
    {
        if (!IsOdfNamespace(reader.NamespaceURI) || schema.ContainsElement(reader.NamespaceURI, reader.LocalName))
        {
            return;
        }

        OdfPolicyRule? rule = FindRule(profile, "DisallowInvalidOdfNamespaceExtensions") ??
            FindRule(profile, "RequireOdfNamespaceValidity");
        if (rule is null)
        {
            return;
        }

        OdfVersion? requiredVersion = FindMinimumElementVersion(reader.NamespaceURI, reader.LocalName);

        issues.Add(new OdfValidationIssue(
            rule.DefaultSeverity,
            rule.Id,
            $"ODF namespace element '{{{reader.NamespaceURI}}}{reader.LocalName}' is not present in the selected schema metadata.",
            packagePath,
            xPath,
            requiredVersion: requiredVersion,
            profileId: profile.Id));
    }

    private static void ValidateOdfNamespaceAttributes(
        XmlReader reader,
        string? packagePath,
        string xPath,
        OdfComplianceProfile profile,
        OdfSchemaSet schema,
        List<OdfValidationIssue> issues)
    {
        OdfPolicyRule? rule = FindRule(profile, "DisallowInvalidOdfNamespaceExtensions") ??
            FindRule(profile, "RequireOdfNamespaceValidity");
        if (rule is null || !reader.HasAttributes)
        {
            return;
        }

        for (int i = 0; i < reader.AttributeCount; i++)
        {
            reader.MoveToAttribute(i);
            if (IsOdfNamespace(reader.NamespaceURI) &&
                schema.FindAttribute(reader.NamespaceURI, reader.LocalName) is null)
            {
                OdfVersion? requiredVersion = FindMinimumAttributeVersion(reader.NamespaceURI, reader.LocalName);

                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    rule.Id,
                    $"ODF namespace attribute '{{{reader.NamespaceURI}}}{reader.LocalName}' is not present in the selected schema metadata.",
                    packagePath,
                    xPath + "/@" + reader.LocalName,
                    requiredVersion: requiredVersion,
                    profileId: profile.Id));
            }
        }

        reader.MoveToElement();
    }

    private static void ValidateForeignExtensionElement(
        XmlReader reader,
        string? packagePath,
        string xPath,
        OdfComplianceProfile profile,
        List<OdfValidationIssue> issues)
    {
        OdfPolicyRule? rule = FindRule(profile, "RequireForeignExtensionIsolation");
        if (rule is null ||
            string.IsNullOrEmpty(reader.NamespaceURI) ||
            IsOdfNamespace(reader.NamespaceURI) ||
            IsKnownInfrastructureNamespace(reader.NamespaceURI))
        {
            return;
        }

        issues.Add(new OdfValidationIssue(
            rule.DefaultSeverity,
            rule.Id,
            $"Foreign extension element '{{{reader.NamespaceURI}}}{reader.LocalName}' should remain isolated and removable.",
            packagePath,
            xPath,
            profileId: profile.Id));
    }

    private static void ValidateMacroOrScriptElement(
        XmlReader reader,
        string? packagePath,
        string xPath,
        OdfComplianceProfile profile,
        List<OdfValidationIssue> issues)
    {
        OdfPolicyRule? macroRule = FindRule(profile, "DisallowMacroByDefault");
        OdfPolicyRule? resourceRule = FindRule(profile, "RequireSafeExternalResourcePolicy");
        OdfPolicyRule? rule = macroRule ?? resourceRule;
        if (rule is null)
        {
            return;
        }

        bool isScriptElement = reader.LocalName.IndexOf("script", StringComparison.OrdinalIgnoreCase) >= 0 ||
            reader.LocalName.IndexOf("event-listener", StringComparison.OrdinalIgnoreCase) >= 0 ||
            reader.NamespaceURI.IndexOf(":script:", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!isScriptElement)
        {
            return;
        }

        issues.Add(new OdfValidationIssue(
            rule.DefaultSeverity,
            rule.Id,
            "Profile reports script or event-listener XML content.",
            packagePath,
            xPath,
            profileId: profile.Id));
    }

    private static void ValidateMacroOrScriptAttributes(
        XmlReader reader,
        string? packagePath,
        string xPath,
        OdfComplianceProfile profile,
        List<OdfValidationIssue> issues)
    {
        OdfPolicyRule? macroRule = FindRule(profile, "DisallowMacroByDefault");
        if (macroRule is null || !reader.HasAttributes)
        {
            return;
        }

        for (int i = 0; i < reader.AttributeCount; i++)
        {
            reader.MoveToAttribute(i);

            string attrQName = string.IsNullOrEmpty(reader.Prefix) ? reader.LocalName : $"{reader.Prefix}:{reader.LocalName}";
            bool isScriptAttr = reader.NamespaceURI == "urn:oasis:names:tc:opendocument:xmlns:script:1.0" ||
                                string.Equals(reader.Prefix, "script", StringComparison.Ordinal) ||
                                string.Equals(attrQName, "script:event-name", StringComparison.Ordinal) ||
                                (string.Equals(reader.LocalName, "event-name", StringComparison.Ordinal) && reader.NamespaceURI == "urn:oasis:names:tc:opendocument:xmlns:script:1.0") ||
                                reader.Value.Contains("vnd.sun.star.script:");

            if (isScriptAttr)
            {
                issues.Add(new OdfValidationIssue(
                    macroRule.DefaultSeverity,
                    macroRule.Id,
                    $"Profile reports script or macro attribute '{attrQName}' with value '{reader.Value}'.",
                    packagePath,
                    xPath,
                    profileId: profile.Id));
            }
        }

        reader.MoveToElement();
    }

    private static void ValidateExternalResourceAttributes(
        XmlReader reader,
        string? packagePath,
        string xPath,
        OdfComplianceProfile profile,
        List<OdfValidationIssue> issues)
    {
        OdfPolicyRule? rule = FindRule(profile, "RequireSafeExternalResourcePolicy") ??
            FindRule(profile, "RequireCrossBorderInteroperability");
        if (rule is null || !reader.HasAttributes)
        {
            return;
        }

        for (int i = 0; i < reader.AttributeCount; i++)
        {
            reader.MoveToAttribute(i);
            if (reader.NamespaceURI == OdfNamespaces.XLink &&
                string.Equals(reader.LocalName, "href", StringComparison.Ordinal) &&
                IsExternalReference(reader.Value))
            {
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    rule.Id,
                    $"Profile reports external resource reference '{reader.Value}'.",
                    packagePath,
                    xPath,
                    profileId: profile.Id));
            }
        }

        reader.MoveToElement();
    }

    private static OdfPolicyRule? FindRule(OdfComplianceProfile profile, string id)
    {
        return profile.Rules.FirstOrDefault(rule => string.Equals(rule.Id, id, StringComparison.Ordinal));
    }

    private static bool IsOdfNamespace(string namespaceUri)
    {
        return namespaceUri.StartsWith("urn:oasis:names:tc:opendocument:xmlns:", StringComparison.Ordinal);
    }

    private static bool IsKnownInfrastructureNamespace(string namespaceUri)
    {
        return namespaceUri == OdfNamespaces.XLink ||
            namespaceUri == OdfNamespaces.Dc ||
            namespaceUri == OdfNamespaces.Ds ||
            namespaceUri == "http://www.w3.org/2001/XMLSchema-instance";
    }

    private static bool IsExternalReference(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        int colonIndex = value.IndexOf(':');
        if (colonIndex > 0)
        {
            // 檢查冒號前的字元是否皆為字母
            for (int i = 0; i < colonIndex; i++)
            {
                char c = value[i];
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
                {
                    return false;
                }
            }
            return true;
        }

        return false;
    }

    private static bool IsStandardNamespace(string namespaceUri)
    {
        return IsOdfNamespace(namespaceUri) ||
            IsKnownInfrastructureNamespace(namespaceUri) ||
            namespaceUri == "http://www.w3.org/1998/Math/MathML" ||
            namespaceUri == "http://www.w3.org/2000/xmlns/" ||
            string.IsNullOrEmpty(namespaceUri);
    }

    private static bool IsInfrastructureOrMathNamespace(string namespaceUri)
    {
        return namespaceUri == "http://www.w3.org/1999/xlink" ||
            namespaceUri == "http://purl.org/dc/elements/1.1/" ||
            namespaceUri == "http://www.w3.org/1998/Math/MathML" ||
            namespaceUri == "http://www.w3.org/XML/1998/namespace";
    }

    private static void SanitizeForeignContentForSchemaValidation(XElement element)
    {
        var defaultSchema = OdfSchemaRegistry.DefaultOdf14;

        // 移除 foreign 屬性
        var foreignAttrs = element.Attributes()
            .Where(a => !a.IsNamespaceDeclaration &&
                        !IsInfrastructureOrMathNamespace(a.Name.NamespaceName) &&
                        (!IsStandardNamespace(a.Name.NamespaceName) ||
                         defaultSchema.FindAttribute(a.Name.NamespaceName, a.Name.LocalName) is null))
            .ToList();
        foreach (var attr in foreignAttrs)
        {
            attr.Remove();
        }

        // 遞迴處理子元素並移除 foreign 子元素
        var children = element.Elements().ToList();
        foreach (var child in children)
        {
            if (IsInfrastructureOrMathNamespace(child.Name.NamespaceName))
            {
                SanitizeForeignContentForSchemaValidation(child);
            }
            else if (!IsStandardNamespace(child.Name.NamespaceName) ||
                     defaultSchema.FindElement(child.Name.NamespaceName, child.Name.LocalName) is null)
            {
                child.Remove();
            }
            else
            {
                SanitizeForeignContentForSchemaValidation(child);
            }
        }
    }

    #endregion
}
