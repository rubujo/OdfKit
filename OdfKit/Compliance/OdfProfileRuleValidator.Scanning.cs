using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using OdfKit.Core;

namespace OdfKit.Compliance;

internal static partial class OdfProfileRuleValidator
{
    #region XML Scanning & Macro Rules

    private static void ValidateMacroEntries(
        OdfPackage package,
        OdfComplianceProfile profile,
        List<OdfValidationIssue> issues)
    {
        OdfPolicyRule? macroRule = FindRule(profile, "DisallowMacroByDefault");
        if (macroRule is null)
        {
            return;
        }

        foreach (string entryName in package.Entries.Keys)
        {
            if (entryName.StartsWith("Basic/", StringComparison.OrdinalIgnoreCase) ||
                entryName.StartsWith("Scripts/", StringComparison.OrdinalIgnoreCase) ||
                entryName.EndsWith("macrosignatures.xml", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new OdfValidationIssue(
                    macroRule.DefaultSeverity,
                    macroRule.Id,
                    "Profile reports embedded macro or script package content.",
                    entryName,
                    profileId: profile.Id));
            }
        }
    }

    private class ValidationStackFrame(string namespaceUri, string localName, string qName, string xpath)
    {
        public string NamespaceUri { get; } = namespaceUri;
        public string LocalName { get; } = localName;
        public string QualifiedName { get; } = qName;
        public string XPath { get; } = xpath;

        // 追蹤此堆疊框架中已見過的子元素 QName 計數
        public Dictionary<string, int> ChildCounts { get; } = new(StringComparer.Ordinal);

        // 無障礙檢查累加器
        public bool HasAltText { get; set; }        // 若元素內找到 svg:title 或 svg:desc 則設為 true
        public bool HasTableHeaderRows { get; set; } // 若 table:table 內找到 table:table-header-rows 則設為 true
        public List<ImageInfo> ChildImages { get; } = [];
    }

    private class ImageInfo(string xpath, bool hasAltText)
    {
        public string XPath { get; } = xpath;
        public bool HasAltText { get; } = hasAltText;
    }

    private static void ScanXml(
        Stream stream,
        string? packagePath,
        OdfComplianceProfile profile,
        OdfSchemaSet schema,
        List<OdfValidationIssue> issues,
        bool closeInput)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreWhitespace = true,
            MaxCharactersFromEntities = 0,
            CloseInput = closeInput
        };

        OdfPolicyRule? accessRule = FindRule(profile, "RequireAccessibilityMetadata");
        Stack<ValidationStackFrame> stack = [];

        using XmlReader reader = XmlReader.Create(stream, settings);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                string prefix = OdfNamespaces.GetPrefix(reader.NamespaceURI);
                if (string.IsNullOrEmpty(prefix))
                {
                    prefix = reader.Prefix;
                }
                string qName = string.IsNullOrEmpty(prefix) ? reader.LocalName : $"{prefix}:{reader.LocalName}";

                // 1. 計算 XPath
                string currentXPath;
                if (stack.Count > 0)
                {
                    var parent = stack.Peek();
                    if (!parent.ChildCounts.TryGetValue(qName, out int count))
                    {
                        count = 0;
                    }
                    count++;
                    parent.ChildCounts[qName] = count;
                    currentXPath = $"{parent.XPath}/{qName}[{count}]";
                }
                else
                {
                    currentXPath = $"/{qName}[1]";
                }

                // 2. 執行驗證檢查
                ValidateOdfNamespaceElement(reader, packagePath, currentXPath, profile, schema, issues);
                ValidateOdfNamespaceAttributes(reader, packagePath, currentXPath, profile, schema, issues);
                ValidateForeignExtensionElement(reader, packagePath, currentXPath, profile, issues);
                ValidateMacroOrScriptElement(reader, packagePath, currentXPath, profile, issues);
                ValidateMacroOrScriptAttributes(reader, packagePath, currentXPath, profile, issues);
                ValidateExternalResourceAttributes(reader, packagePath, currentXPath, profile, issues);

                // 3. 處理無障礙中介資料累加
                if (accessRule is not null && stack.Count > 0)
                {
                    var parent = stack.Peek();
                    if (reader.NamespaceURI == OdfNamespaces.Svg &&
                        (reader.LocalName == "title" || reader.LocalName == "desc"))
                    {
                        parent.HasAltText = true;
                    }
                    else if (reader.NamespaceURI == OdfNamespaces.Table &&
                             reader.LocalName == "table-header-rows" &&
                             parent.LocalName == "table" && parent.NamespaceUri == OdfNamespaces.Table)
                    {
                        parent.HasTableHeaderRows = true;
                    }
                }

                // 4. 處理堆疊推送／空元素
                if (reader.IsEmptyElement)
                {
                    if (accessRule is not null && reader.NamespaceURI == OdfNamespaces.Draw && reader.LocalName == "image")
                    {
                        if (stack.Count > 0 && stack.Peek().LocalName == "frame" && stack.Peek().NamespaceUri == OdfNamespaces.Draw)
                        {
                            stack.Peek().ChildImages.Add(new ImageInfo(currentXPath, hasAltText: false));
                        }
                        else
                        {
                            // 無替代文字的獨立圖片
                            issues.Add(new OdfValidationIssue(
                                accessRule.DefaultSeverity,
                                accessRule.Id,
                                "Image is missing alternative text (svg:title or svg:desc).",
                                packagePath,
                                currentXPath,
                                profileId: profile.Id));
                        }
                    }
                }
                else
                {
                    stack.Push(new ValidationStackFrame(reader.NamespaceURI, reader.LocalName, qName, currentXPath));
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                if (stack.Count == 0)
                    continue;
                var frame = stack.Pop();

                if (accessRule is not null)
                {
                    if (frame.NamespaceUri == OdfNamespaces.Draw && frame.LocalName == "image")
                    {
                        if (stack.Count > 0 && stack.Peek().LocalName == "frame" && stack.Peek().NamespaceUri == OdfNamespaces.Draw)
                        {
                            stack.Peek().ChildImages.Add(new ImageInfo(frame.XPath, frame.HasAltText));
                        }
                        else if (!frame.HasAltText)
                        {
                            // 無替代文字的獨立圖片
                            issues.Add(new OdfValidationIssue(
                                accessRule.DefaultSeverity,
                                accessRule.Id,
                                "Image is missing alternative text (svg:title or svg:desc).",
                                packagePath,
                                frame.XPath,
                                profileId: profile.Id));
                        }
                    }
                    else if (frame.NamespaceUri == OdfNamespaces.Draw && frame.LocalName == "frame")
                    {
                        if (!frame.HasAltText)
                        {
                            foreach (var img in frame.ChildImages)
                            {
                                if (!img.HasAltText)
                                {
                                    issues.Add(new OdfValidationIssue(
                                        accessRule.DefaultSeverity,
                                        accessRule.Id,
                                        "Image is missing alternative text (svg:title or svg:desc) on both the draw:image and its parent draw:frame.",
                                        packagePath,
                                        img.XPath,
                                        profileId: profile.Id));
                                }
                            }
                        }
                    }
                    else if (frame.NamespaceUri == OdfNamespaces.Table && frame.LocalName == "table")
                    {
                        if (!frame.HasTableHeaderRows)
                        {
                            issues.Add(new OdfValidationIssue(
                                accessRule.DefaultSeverity,
                                accessRule.Id,
                                "Table is missing a table:table-header-rows child element.",
                                packagePath,
                                frame.XPath,
                                profileId: profile.Id));
                        }
                    }
                }
            }
        }
    }


    #endregion
}
