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

        bool isScriptElement = global::OdfKit.Internal.OdfStringHelper.Contains(reader.LocalName, "script", StringComparison.OrdinalIgnoreCase) ||
            global::OdfKit.Internal.OdfStringHelper.Contains(reader.LocalName, "event-listener", StringComparison.OrdinalIgnoreCase) ||
            global::OdfKit.Internal.OdfStringHelper.Contains(reader.NamespaceURI, ":script:", StringComparison.OrdinalIgnoreCase);
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
            // 「vnd.sun.star.script:」巨集 URI 只可能出現在連結類屬性（如 xlink:href）中，
            // 限縮子字串掃描範圍至此類屬性，避免對文件中每個元素的每個屬性值都做全字串掃描。
            bool isHrefLikeAttr = string.Equals(reader.LocalName, "href", StringComparison.Ordinal);
            bool isScriptAttr = reader.NamespaceURI == OdfNamespaces.Script ||
                                (isHrefLikeAttr && reader.Value.Contains("vnd.sun.star.script:"));

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

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<OdfSchemaSet, HashSet<XName>> TextPermittedElements = new();

    private static HashSet<XName> BuildTextPermittedElements(OdfSchemaSet schema)
    {
        var names = new HashSet<XName>();
        var pending = new Stack<OdfSchemaPatternNode>();
        foreach (OdfSchemaPatternDefinition pattern in schema.Patterns.Values)
            foreach (OdfSchemaPatternNode root in pattern.Roots)
                pending.Push(root);

        var context = new OdfSchemaPatternMatchContext(schema);
        while (pending.Count > 0)
        {
            OdfSchemaPatternNode node = pending.Pop();
            if (node.Kind == OdfSchemaPatternNodeKind.Element)
            {
                if (!string.IsNullOrEmpty(node.LocalName) &&
                    OdfSchemaPatternContentMatcher.ContentAllowsDirectText(node.Children, false, context))
                    names.Add(XName.Get(node.LocalName, node.NamespaceUri));
                continue;
            }
            if (node.Kind == OdfSchemaPatternNodeKind.Attribute)
                continue;
            foreach (OdfSchemaPatternNode child in node.Children)
                pending.Push(child);
        }
        return names;
    }

    private static bool IsSchemaDefinedXhtmlAttribute(XName name) =>
        name.NamespaceName == "http://www.w3.org/1999/xhtml" &&
        name.LocalName is "about" or "property" or "content" or "datatype";

    private static void SanitizeForeignContentForSchemaValidation(
        XElement element,
        OdfSchemaSet schema,
        HashSet<XName> textPermittedElements,
        bool inlineContext = false,
        OdfSchemaNamespaceContext? parentNamespaceContext = null)
    {
        var namespaceContext = new OdfSchemaNamespaceContext(element, parentNamespaceContext);
        element.AddAnnotation(namespaceContext);
        // 移除 foreign 屬性
        // xhtml:about、xhtml:property、xhtml:content、xhtml:datatype 由 ODF schema 定義（RDFa），
        // 不是 foreign 屬性；先剝除會讓只寫一半的 RDFa 屬性組在 Extended 被接受。
        var foreignAttrs = element.Attributes()
            .Where(a => !a.IsNamespaceDeclaration &&
                        !IsInfrastructureOrMathNamespace(a.Name.NamespaceName) &&
                        !IsStandardNamespace(a.Name.NamespaceName) &&
                        !IsSchemaDefinedXhtmlAttribute(a.Name))
            .ToList();
        foreach (var attr in foreignAttrs)
        {
            attr.Remove();
        }

        bool textAnchor = element.Name.NamespaceName == OdfNamespaces.Text &&
            (element.Name.LocalName is "p" or "h" ||
             (schema.Version >= OdfVersion.Odf13 && element.Name.LocalName == "ruby-base"));
        inlineContext = (inlineContext || textAnchor) &&
            (!IsStandardNamespace(element.Name.NamespaceName) || textPermittedElements.Contains(element.Name));

        // inline foreign 元素只移除標籤，內容仍須通過 grammar；其他位置移除整個子樹。
        var children = element.Elements().ToList();
        foreach (var child in children)
        {
            if (!IsStandardNamespace(child.Name.NamespaceName))
            {
                if (inlineContext)
                {
                    SanitizeForeignContentForSchemaValidation(child, schema, textPermittedElements, true, namespaceContext);
                    List<XNode> nodes = child.Nodes().ToList();
                    child.RemoveNodes();
                    child.ReplaceWith(nodes);
                }
                else
                {
                    child.Remove();
                }
            }
            else
            {
                SanitizeForeignContentForSchemaValidation(child, schema, textPermittedElements, inlineContext, namespaceContext);
            }
        }
    }

    #endregion
}
