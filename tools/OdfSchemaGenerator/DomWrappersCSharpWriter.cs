using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OdfKit.Tools.OdfSchemaGenerator;

public sealed class DomWrappersCSharpWriter
{
    private static readonly HashSet<string> HandWrittenClasses = new HashSet<string>(StringComparer.Ordinal)
    {
        "TextPElement", "TextHElement", "TextSpanElement", "TextListElement", "TextListItemElement",
        "TextSectionElement", "TextBookmarkElement", "TextNoteElement", "OfficeAnnotationElement",
        "TableTableElement", "TableTableRowElement", "TableTableCellElement", "TableCoveredTableCellElement",
        "TableNamedRangeElement", "TableDatabaseRangeElement",
        "DrawFrameElement", "DrawImageElement", "DrawObjectElement", "DrawShapeElement", "DrawGroupElement", "DrawConnectorElement",
        "StyleStyleElement", "StyleDefaultStyleElement", "StyleMasterPageElement", "StylePageLayoutElement",
        "StyleTextPropertiesElement", "StyleParagraphPropertiesElement",
        "OfficeDocumentElement", "OfficeDocumentContentElement", "OfficeBodyElement", "OfficeTextElement",
        "OfficeSpreadsheetElement", "OfficePresentationElement", "OfficeDrawingElement",
        "ManifestManifestElement", "ManifestFileEntryElement", "ManifestEncryptionDataElement"
    };

    public void Write(SchemaMetadata metadata, TextWriter writer)
    {
        var elementAttributes = ResolveAllElementAttributes(metadata);
        var elementChildRelations = ResolveAllElementChildRelations(metadata);
        var sortedElements = metadata.Elements
            .OrderBy(e => e.NamespaceUri, StringComparer.Ordinal)
            .ThenBy(e => e.LocalName, StringComparer.Ordinal)
            .ToList();

        WriteFileHeader(writer);
        writer.WriteLine("namespace OdfKit.DOM");
        writer.WriteLine("{");
        WriteElementWrappers(writer, sortedElements, elementAttributes, elementChildRelations);
        WriteHandWrittenPartialExtensions(writer, sortedElements, elementChildRelations);
        WriteFactory(writer, sortedElements);
        writer.WriteLine("}");
    }

    public void WriteToDirectory(SchemaMetadata metadata, string outputDirectory)
    {
        if (metadata is null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));
        }

        // 清理舊有的所有產生程式碼，避免重複定義衝突
        if (Directory.Exists(outputDirectory))
        {
            foreach (string file in Directory.GetFiles(outputDirectory, "*.g.cs"))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                    // 忽略暫時的鎖定
                }
            }
        }
        else
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var elementAttributes = ResolveAllElementAttributes(metadata);
        var elementChildRelations = ResolveAllElementChildRelations(metadata);
        var sortedElements = metadata.Elements
            .OrderBy(e => e.NamespaceUri, StringComparer.Ordinal)
            .ThenBy(e => e.LocalName, StringComparer.Ordinal)
            .ToList();

        // 依型別一檔案 (Type-per-file) 產生強型別 Wrappers
        foreach (var element in sortedElements)
        {
            string nsPrefix = GetNamespacePascalName(element.NamespaceUri);
            string localPascal = ToPascalCase(element.LocalName);
            string className = nsPrefix + localPascal + "Element";

            bool isHandWritten = HandWrittenClasses.Contains(className);
            bool hasChildRelations = elementChildRelations.TryGetValue((element.NamespaceUri, element.LocalName), out var childRelations);

            // 若為手寫類別且無子關係，則完全不需產生任何 wrapper 檔
            if (isHandWritten && !hasChildRelations)
            {
                continue;
            }

            string fileName = className + ".g.cs";
            string outputPath = Path.Combine(outputDirectory, fileName);
            using var writer = new StreamWriter(outputPath, append: false, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            WriteFileHeader(writer);
            writer.WriteLine("namespace OdfKit.DOM");
            writer.WriteLine("{");

            if (!isHandWritten)
            {
                WriteElementWrappers(writer, new[] { element }, elementAttributes, elementChildRelations);
            }
            else
            {
                WriteHandWrittenPartialExtensions(writer, new[] { element }, elementChildRelations);
            }

            writer.WriteLine("}");
        }

        string factoryPath = Path.Combine(outputDirectory, "GeneratedDomFactory.g.cs");
        using var factoryWriter = new StreamWriter(factoryPath, append: false, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        WriteFileHeader(factoryWriter);
        factoryWriter.WriteLine("namespace OdfKit.DOM");
        factoryWriter.WriteLine("{");
        WriteFactory(factoryWriter, sortedElements);
        factoryWriter.WriteLine("}");
    }

    private static Dictionary<string, string> BuildNamespaceFileKeys(IEnumerable<string> namespaceUris)
    {
        var namespaceFileKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        var usedKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (string namespaceUri in namespaceUris.OrderBy(item => item, StringComparer.Ordinal))
        {
            string baseKey = NormalizeFileKey(GetNamespacePascalName(namespaceUri));
            string key = baseKey;
            int suffix = 2;
            while (!usedKeys.Add(key))
            {
                key = baseKey + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }

            namespaceFileKeys[namespaceUri] = key;
        }

        return namespaceFileKeys;
    }

    private static string NormalizeFileKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "ns";
        }

        var chars = new List<char>(value.Length);
        foreach (char ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                chars.Add(char.ToLowerInvariant(ch));
            }
        }

        return chars.Count > 0 ? new string(chars.ToArray()) : "ns";
    }

    private static void WriteFileHeader(TextWriter writer)
    {
        writer.WriteLine("// <auto-generated />");
        writer.WriteLine("// Generated from OASIS Open Document Format (ODF) schemas.");
        writer.WriteLine("// Copyright (c) OASIS Open. All rights reserved.");
        writer.WriteLine("#nullable enable");
        writer.WriteLine("#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for generated DOM wrappers as they are machine-generated");
        writer.WriteLine("using System;");
        writer.WriteLine("using System.Collections.Generic;");
        writer.WriteLine("using OdfKit.Core;");
        writer.WriteLine("using OdfKit.Compliance;");
        writer.WriteLine("using OdfKit.Styles;");
        writer.WriteLine();
    }

    private static void WriteElementWrappers(
        TextWriter writer,
        IEnumerable<SchemaNameMetadata> elements,
        IReadOnlyDictionary<(string, string), List<AttributePropertyMetadata>> elementAttributes,
        IReadOnlyDictionary<(string, string), List<ChildElementPropertyMetadata>> elementChildRelations)
    {
        foreach (var element in elements)
        {
            string nsPrefix = GetNamespacePascalName(element.NamespaceUri);
            string localPascal = ToPascalCase(element.LocalName);
            string className = nsPrefix + localPascal + "Element";
            if (HandWrittenClasses.Contains(className))
            {
                continue;
            }

            writer.WriteLine("    /// <summary>");
            writer.WriteLine($"    /// Typed wrapper for element &lt;{GetPrefix(element.NamespaceUri)}:{element.LocalName}&gt;.");
            writer.WriteLine("    /// </summary>");
            writer.WriteLine($"    public partial class {className} : OdfElement");
            writer.WriteLine("    {");
            writer.WriteLine($"        public {className}(string? prefix = null) : base(\"{element.LocalName}\", \"{element.NamespaceUri}\", prefix) {{ }}");

            var existingPropertyNames = new List<string>();
            if (elementAttributes.TryGetValue((element.NamespaceUri, element.LocalName), out var attrs))
            {
                var resolvedNames = ResolvePropertyNames(attrs);
                existingPropertyNames.AddRange(resolvedNames.Values);
                foreach (var attr in attrs.OrderBy(item => resolvedNames[item], StringComparer.Ordinal))
                {
                    string propName = resolvedNames[attr];
                    string prefix = GetPrefix(attr.NamespaceUri);
                    WriteAttributeProperty(writer, attr, propName, prefix);
                }
            }

            if (elementChildRelations.TryGetValue((element.NamespaceUri, element.LocalName), out var childRelations))
            {
                WriteChildElementProperties(writer, childRelations, existingPropertyNames);
            }

            writer.WriteLine("    }");
            writer.WriteLine();
        }
    }

    private static void WriteHandWrittenPartialExtensions(
        TextWriter writer,
        IEnumerable<SchemaNameMetadata> elements,
        IReadOnlyDictionary<(string, string), List<ChildElementPropertyMetadata>> elementChildRelations)
    {
        foreach (var element in elements)
        {
            string nsPrefix = GetNamespacePascalName(element.NamespaceUri);
            string localPascal = ToPascalCase(element.LocalName);
            string className = nsPrefix + localPascal + "Element";

            if (!HandWrittenClasses.Contains(className) ||
                !elementChildRelations.TryGetValue((element.NamespaceUri, element.LocalName), out var childRelations))
            {
                continue;
            }

            writer.WriteLine($"    public partial class {className}");
            writer.WriteLine("    {");
            WriteChildElementProperties(writer, childRelations, GetHandWrittenPropertyNames(className));
            writer.WriteLine("    }");
            writer.WriteLine();
        }
    }

    private static void WriteFactory(TextWriter writer, IReadOnlyList<SchemaNameMetadata> sortedElements)
    {
        writer.WriteLine("#if NET8_0_OR_GREATER");
        writer.WriteLine("using System.Collections.Frozen;");
        writer.WriteLine("#endif");
        writer.WriteLine("using System.Collections.Generic;");
        writer.WriteLine();
        writer.WriteLine("    public static partial class OdfNodeFactory");
        writer.WriteLine("    {");
        writer.WriteLine("#if NET8_0_OR_GREATER");
        writer.WriteLine("        private static readonly FrozenDictionary<string, FrozenDictionary<string, Func<string?, OdfNode>>> GeneratedFactories;");
        writer.WriteLine("#else");
        writer.WriteLine("        private static readonly Dictionary<string, Dictionary<string, Func<string?, OdfNode>>> GeneratedFactories;");
        writer.WriteLine("#endif");
        writer.WriteLine();
        writer.WriteLine("        static OdfNodeFactory()");
        writer.WriteLine("        {");
        writer.WriteLine("            var temp = new Dictionary<string, Dictionary<string, Func<string?, OdfNode>>>(StringComparer.Ordinal);");
        writer.WriteLine();

        var groups = sortedElements.GroupBy(element => element.NamespaceUri).ToList();
        foreach (var group in groups)
        {
            string nsKey = NormalizeFileKey(GetNamespacePascalName(group.Key));
            writer.WriteLine($"            var nsMap_{nsKey} = new Dictionary<string, Func<string?, OdfNode>>(StringComparer.Ordinal)");
            writer.WriteLine("            {");
            foreach (var element in group)
            {
                string nsPrefix = GetNamespacePascalName(element.NamespaceUri);
                string localPascal = ToPascalCase(element.LocalName);
                string className = nsPrefix + localPascal + "Element";
                writer.WriteLine($"                {{ \"{element.LocalName}\", prefix => new {className}(prefix) }},");
            }
            writer.WriteLine("            };");
            writer.WriteLine($"            temp[\"{group.Key}\"] = nsMap_{nsKey};");
            writer.WriteLine();
        }

        writer.WriteLine("#if NET8_0_OR_GREATER");
        writer.WriteLine("            GeneratedFactories = temp.ToFrozenDictionary(");
        writer.WriteLine("                pair => pair.Key,");
        writer.WriteLine("                pair => pair.Value.ToFrozenDictionary(StringComparer.Ordinal),");
        writer.WriteLine("                StringComparer.Ordinal");
        writer.WriteLine("            );");
        writer.WriteLine("#else");
        writer.WriteLine("            GeneratedFactories = temp;");
        writer.WriteLine("#endif");
        writer.WriteLine("        }");
        writer.WriteLine();
        writer.WriteLine("        public static OdfNode? CreateGeneratedElement(string localName, string namespaceUri, string? prefix)");
        writer.WriteLine("        {");
        writer.WriteLine("            if (GeneratedFactories.TryGetValue(namespaceUri, out var nsFactories) &&");
        writer.WriteLine("                nsFactories.TryGetValue(localName, out var factory))");
        writer.WriteLine("            {");
        writer.WriteLine("                return factory(prefix);");
        writer.WriteLine("            }");
        writer.WriteLine("            return null;");
        writer.WriteLine("        }");
        writer.WriteLine("    }");
    }

    private static Dictionary<(string, string), List<AttributePropertyMetadata>> ResolveAllElementAttributes(SchemaMetadata metadata)
    {
        var elementAttributes = new Dictionary<(string, string), List<AttributePropertyMetadata>>();
        var elementNodes = new List<SchemaPatternNodeMetadata>();

        foreach (var pattern in metadata.Patterns)
        {
            foreach (var node in pattern.PatternTree)
            {
                FindElementNodes(node, elementNodes);
            }
        }

        foreach (var node in elementNodes)
        {
            var key = (node.NamespaceUri, node.LocalName);
            if (!elementAttributes.TryGetValue(key, out var attrs))
            {
                attrs = new List<AttributePropertyMetadata>();
                elementAttributes[key] = attrs;
            }

            var collected = new List<AttributePropertyMetadata>();
            CollectAttributes(node, collected, metadata, new HashSet<string>());

            foreach (var attr in collected)
            {
                AttributePropertyMetadata? existing = attrs.FirstOrDefault(a =>
                    a.NamespaceUri == attr.NamespaceUri &&
                    a.LocalName == attr.LocalName);
                if (existing is null)
                {
                    attrs.Add(attr);
                    continue;
                }

                existing.ValueKind = MergeValueKinds(existing.ValueKind, attr.ValueKind);
            }
        }

        return elementAttributes;
    }

    private static Dictionary<(string, string), List<ChildElementPropertyMetadata>> ResolveAllElementChildRelations(SchemaMetadata metadata)
    {
        var relations = new Dictionary<(string, string), List<ChildElementPropertyMetadata>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var pattern in metadata.Patterns)
        {
            foreach (var node in pattern.PatternTree)
            {
                CollectParentElementChildRelations(node, metadata, relations, seen, new HashSet<string>());
            }
        }

        return relations;
    }

    private static void CollectParentElementChildRelations(
        SchemaPatternNodeMetadata node,
        SchemaMetadata metadata,
        Dictionary<(string, string), List<ChildElementPropertyMetadata>> relations,
        HashSet<string> seen,
        HashSet<string> visitedRefs)
    {
        if (node.Kind == "element")
        {
            CollectDirectChildElementRelations(node, metadata, relations, seen, new HashSet<string>());
            return;
        }

        if (node.Kind == "ref" && !string.IsNullOrEmpty(node.ReferenceName) && visitedRefs.Add(node.ReferenceName))
        {
            var refPattern = metadata.Patterns.FirstOrDefault(pattern => pattern.Name == node.ReferenceName);
            if (refPattern is not null)
            {
                foreach (var refNode in refPattern.PatternTree)
                {
                    CollectParentElementChildRelations(refNode, metadata, relations, seen, visitedRefs);
                }
            }

            visitedRefs.Remove(node.ReferenceName);
            return;
        }

        foreach (var child in node.Children)
        {
            CollectParentElementChildRelations(child, metadata, relations, seen, visitedRefs);
        }
    }

    private static void CollectDirectChildElementRelations(
        SchemaPatternNodeMetadata parent,
        SchemaMetadata metadata,
        Dictionary<(string, string), List<ChildElementPropertyMetadata>> relations,
        HashSet<string> seen,
        HashSet<string> visitedRefs)
    {
        foreach (var child in parent.Children)
        {
            CollectDirectChildElementRelations(parent, child, metadata, relations, seen, visitedRefs);
        }
    }

    private static void CollectDirectChildElementRelations(
        SchemaPatternNodeMetadata parent,
        SchemaPatternNodeMetadata node,
        SchemaMetadata metadata,
        Dictionary<(string, string), List<ChildElementPropertyMetadata>> relations,
        HashSet<string> seen,
        HashSet<string> visitedRefs)
    {
        if (node.Kind == "attribute")
        {
            return;
        }

        if (node.Kind == "element")
        {
            if (!string.IsNullOrEmpty(parent.NamespaceUri) &&
                !string.IsNullOrEmpty(parent.LocalName) &&
                !string.IsNullOrEmpty(node.NamespaceUri) &&
                !string.IsNullOrEmpty(node.LocalName))
            {
                string key = string.Join(
                    "\u001f",
                    parent.NamespaceUri,
                    parent.LocalName,
                    node.NamespaceUri,
                    node.LocalName);
                if (seen.Add(key))
                {
                    var parentKey = (parent.NamespaceUri, parent.LocalName);
                    if (!relations.TryGetValue(parentKey, out var children))
                    {
                        children = new List<ChildElementPropertyMetadata>();
                        relations[parentKey] = children;
                    }

                    children.Add(new ChildElementPropertyMetadata
                    {
                        NamespaceUri = node.NamespaceUri,
                        LocalName = node.LocalName
                    });
                }
            }

            return;
        }

        if (node.Kind == "ref" && !string.IsNullOrEmpty(node.ReferenceName) && visitedRefs.Add(node.ReferenceName))
        {
            var refPattern = metadata.Patterns.FirstOrDefault(pattern => pattern.Name == node.ReferenceName);
            if (refPattern is not null)
            {
                foreach (var refNode in refPattern.PatternTree)
                {
                    CollectDirectChildElementRelations(parent, refNode, metadata, relations, seen, visitedRefs);
                }
            }

            visitedRefs.Remove(node.ReferenceName);
            return;
        }

        foreach (var child in node.Children)
        {
            CollectDirectChildElementRelations(parent, child, metadata, relations, seen, visitedRefs);
        }
    }

    private static void WriteChildElementProperties(
        TextWriter writer,
        List<ChildElementPropertyMetadata> childRelations,
        IEnumerable<string> existingNames)
    {
        var usedNames = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase)
        {
            "NodeType", "LocalName", "NamespaceUri", "Prefix", "Parent", "Children",
            "Attributes", "TextContent", "CloneNode", "ImportNode", "GetAttribute",
            "SetAttribute", "RemoveAttribute", "AppendChild", "InsertBefore", "InsertAfter",
            "RemoveChild", "GetDocumentVersion", "GetAttributeValue", "SetAttributeValue",
            "ChildElements", "DescendantElements", "AppendElement", "InsertElementBefore",
            "InsertElementAfter"
        };

        foreach (var child in childRelations
            .OrderBy(relation => relation.NamespaceUri, StringComparer.Ordinal)
            .ThenBy(relation => relation.LocalName, StringComparer.Ordinal))
        {
            string childClassName = GetElementClassName(child.NamespaceUri, child.LocalName);
            string propertyName = childClassName.EndsWith("Element", StringComparison.Ordinal)
                ? childClassName.Substring(0, childClassName.Length - "Element".Length) + "ChildElements"
                : childClassName + "ChildElements";
            if (!usedNames.Add(propertyName))
            {
                propertyName = childClassName + "SchemaChildElements";
                int counter = 1;
                while (!usedNames.Add(propertyName))
                {
                    propertyName = childClassName + "SchemaChildElements" + counter.ToString(CultureInfo.InvariantCulture);
                    counter++;
                }
            }

            writer.WriteLine();
            writer.WriteLine($"        public IEnumerable<{childClassName}> {propertyName}");
            writer.WriteLine("        {");
            writer.WriteLine($"            get => ChildElements<{childClassName}>();");
            writer.WriteLine("        }");
        }
    }

    private static IEnumerable<string> GetHandWrittenPropertyNames(string className)
    {
        return className switch
        {
            "TextPElement" => ["StyleName"],
            "TextHElement" => ["StyleName", "OutlineLevel"],
            "TextSpanElement" => ["StyleName"],
            "TextListElement" => ["StyleName"],
            "TextListItemElement" => ["StartValue"],
            "TextSectionElement" => ["Name", "StyleName"],
            "TextBookmarkElement" => ["Name"],
            "TextNoteElement" => ["NoteClass", "Id"],
            "OfficeAnnotationElement" => ["Creator", "Date"],
            "TableTableElement" => ["Name", "StyleName"],
            "TableTableRowElement" => ["StyleName"],
            "TableTableCellElement" => ["ValueType", "Value", "Formula"],
            "TableCoveredTableCellElement" => ["ValueType"],
            "TableNamedRangeElement" => ["Name", "CellRangeAddress"],
            "TableDatabaseRangeElement" => ["Name", "TargetRangeAddress"],
            "DrawFrameElement" => ["Name", "StyleName", "TextAnchorType", "X", "Y", "Width", "Height"],
            "DrawImageElement" => ["Href", "Type", "Show", "Actuate"],
            "DrawObjectElement" => ["Href", "Type"],
            "DrawShapeElement" => ["StyleName"],
            "DrawGroupElement" => ["StyleName"],
            "DrawConnectorElement" => ["StyleName"],
            "StyleStyleElement" => ["Name", "Family", "ParentStyleName"],
            "StyleDefaultStyleElement" => ["Family"],
            "StyleMasterPageElement" => ["Name", "PageLayoutName"],
            "StylePageLayoutElement" => ["Name"],
            "OfficeDocumentElement" => ["Version"],
            "OfficeDocumentContentElement" => ["Version"],
            "ManifestManifestElement" => ["Version"],
            "ManifestFileEntryElement" => ["FullPath", "MediaType"],
            "ManifestEncryptionDataElement" => ["ChecksumType", "Checksum"],
            _ => []
        };
    }

    private static void FindElementNodes(SchemaPatternNodeMetadata node, List<SchemaPatternNodeMetadata> elementNodes)
    {
        if (node.Kind == "element")
        {
            elementNodes.Add(node);
        }
        foreach (var child in node.Children)
        {
            FindElementNodes(child, elementNodes);
        }
    }

    private static void CollectAttributes(SchemaPatternNodeMetadata node, List<AttributePropertyMetadata> attrs, SchemaMetadata metadata, HashSet<string> visitedRefs)
    {
        if (node.Kind == "attribute" && !string.IsNullOrEmpty(node.LocalName))
        {
            attrs.Add(new AttributePropertyMetadata
            {
                NamespaceUri = node.NamespaceUri,
                LocalName = node.LocalName,
                ValueKind = InferAttributeValueKind(node, metadata, new HashSet<string>())
            });
        }
        else if (node.Kind == "ref" && !string.IsNullOrEmpty(node.ReferenceName) && visitedRefs.Add(node.ReferenceName))
        {
            var refPattern = metadata.Patterns.FirstOrDefault(p => p.Name == node.ReferenceName);
            if (refPattern != null)
            {
                foreach (var refNode in refPattern.PatternTree)
                {
                    CollectAttributes(refNode, attrs, metadata, visitedRefs);
                }
            }
        }
        foreach (var child in node.Children)
        {
            CollectAttributes(child, attrs, metadata, visitedRefs);
        }
    }

    private static Dictionary<AttributePropertyMetadata, string> ResolvePropertyNames(List<AttributePropertyMetadata> attrs)
    {
        var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "NodeType", "LocalName", "NamespaceUri", "Prefix", "Parent", "Children",
            "Attributes", "TextContent", "CloneNode", "ImportNode", "GetAttribute",
            "SetAttribute", "RemoveAttribute", "AppendChild", "InsertBefore", "InsertAfter",
            "RemoveChild", "GetDocumentVersion", "GetAttributeValue", "SetAttributeValue"
        };

        var usedNames = new HashSet<string>(reservedNames, StringComparer.OrdinalIgnoreCase);
        var propNames = new Dictionary<AttributePropertyMetadata, string>();

        var localNameGroups = attrs.GroupBy(a => ToPascalCase(a.LocalName)).ToList();
        foreach (var group in localNameGroups)
        {
            if (group.Count() == 1)
            {
                var attr = group.First();
                string name = ToPascalCase(attr.LocalName);
                if (usedNames.Add(name))
                {
                    propNames[attr] = name;
                }
                else
                {
                    string altName = GetNamespacePascalName(attr.NamespaceUri) + name;
                    if (usedNames.Add(altName))
                    {
                        propNames[attr] = altName;
                    }
                    else
                    {
                        altName += "Attribute";
                        int counter = 1;
                        while (!usedNames.Add(altName))
                        {
                            altName = GetNamespacePascalName(attr.NamespaceUri) + name + "Attribute" + counter;
                            counter++;
                        }
                        propNames[attr] = altName;
                    }
                }
            }
            else
            {
                foreach (var attr in group)
                {
                    string name = GetNamespacePascalName(attr.NamespaceUri) + ToPascalCase(attr.LocalName);
                    if (usedNames.Add(name))
                    {
                        propNames[attr] = name;
                    }
                    else
                    {
                        name += "Attribute";
                        int counter = 1;
                        while (!usedNames.Add(name))
                        {
                            name = GetNamespacePascalName(attr.NamespaceUri) + ToPascalCase(attr.LocalName) + "Attribute" + counter;
                            counter++;
                        }
                        propNames[attr] = name;
                    }
                }
            }
        }

        return propNames;
    }

    private static AttributeValueKind InferAttributeValueKind(SchemaPatternNodeMetadata node, SchemaMetadata metadata, HashSet<string> visitedRefs)
    {
        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:style:1.0" &&
            node.LocalName == "family")
        {
            return AttributeValueKind.StyleFamily;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:office:1.0" &&
            node.LocalName == "version")
        {
            return AttributeValueKind.OdfVersion;
        }

        if (node.LocalName == "media-type" ||
            (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:office:1.0" &&
                node.LocalName == "mimetype"))
        {
            return AttributeValueKind.MediaType;
        }

        if (node.NamespaceUri == "http://www.w3.org/1999/xlink" &&
            node.LocalName == "type")
        {
            return AttributeValueKind.XLinkType;
        }

        if (node.NamespaceUri == "http://www.w3.org/1999/xlink" &&
            node.LocalName == "show")
        {
            return AttributeValueKind.XLinkShow;
        }

        if (node.NamespaceUri == "http://www.w3.org/1999/xlink" &&
            node.LocalName == "actuate")
        {
            return AttributeValueKind.XLinkActuate;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0" &&
            node.LocalName == "style")
        {
            return AttributeValueKind.NumberStyle;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0" &&
            node.LocalName == "calendar")
        {
            return AttributeValueKind.NumberCalendar;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:table:1.0" &&
            node.LocalName == "order")
        {
            return AttributeValueKind.TableOrder;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:table:1.0" &&
            node.LocalName == "type")
        {
            return AttributeValueKind.TableType;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0" &&
            node.LocalName == "effect")
        {
            return AttributeValueKind.PresentationEffect;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0" &&
            (node.LocalName == "speed" || node.LocalName == "transition-speed"))
        {
            return AttributeValueKind.PresentationSpeed;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0" &&
            node.LocalName == "action")
        {
            return AttributeValueKind.PresentationAction;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0" &&
            node.LocalName == "transition-type")
        {
            return AttributeValueKind.PresentationTransitionType;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0" &&
            node.LocalName == "transition-style")
        {
            return AttributeValueKind.PresentationTransitionStyle;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" &&
            node.LocalName == "text-transform")
        {
            return AttributeValueKind.FoTextTransform;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" &&
            node.LocalName == "text-align")
        {
            return AttributeValueKind.FoTextAlign;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:style:1.0" &&
            node.LocalName == "text-rotation-scale")
        {
            return AttributeValueKind.StyleTextRotationScale;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:style:1.0" &&
            node.LocalName == "text-combine")
        {
            return AttributeValueKind.StyleTextCombine;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0" &&
            node.LocalName == "fill")
        {
            return AttributeValueKind.DrawFill;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0" &&
            node.LocalName == "fill")
        {
            return AttributeValueKind.SmilFill;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0" &&
            node.LocalName == "fill-image-ref-point")
        {
            return AttributeValueKind.DrawFillImageRefPoint;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0" &&
            node.LocalName == "color-mode")
        {
            return AttributeValueKind.DrawColorMode;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:style:1.0" &&
            node.LocalName == "vertical-align")
        {
            return AttributeValueKind.StyleVerticalAlign;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:style:1.0" &&
            node.LocalName == "vertical-pos")
        {
            return AttributeValueKind.StyleVerticalPos;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:style:1.0" &&
            node.LocalName == "vertical-rel")
        {
            return AttributeValueKind.StyleVerticalRel;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:style:1.0" &&
            node.LocalName == "horizontal-pos")
        {
            return AttributeValueKind.StyleHorizontalPos;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:style:1.0" &&
            node.LocalName == "horizontal-rel")
        {
            return AttributeValueKind.StyleHorizontalRel;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:style:1.0" &&
            node.LocalName == "wrap")
        {
            return AttributeValueKind.StyleWrap;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:style:1.0" &&
            node.LocalName == "run-through")
        {
            return AttributeValueKind.StyleRunThrough;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:style:1.0" &&
            node.LocalName == "wrap-contour-mode")
        {
            return AttributeValueKind.StyleWrapContourMode;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:style:1.0" &&
            node.LocalName == "writing-mode")
        {
            return AttributeValueKind.StyleWritingMode;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:table:1.0" &&
            node.LocalName == "display-member-mode")
        {
            return AttributeValueKind.TableDisplayMemberMode;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:table:1.0" &&
            node.LocalName == "layout-mode")
        {
            return AttributeValueKind.TableLayoutMode;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:table:1.0" &&
            node.LocalName == "member-type")
        {
            return AttributeValueKind.TableMemberType;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:table:1.0" &&
            node.LocalName == "grouped-by")
        {
            return AttributeValueKind.TableGroupedBy;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:table:1.0" &&
            node.LocalName == "sort-mode")
        {
            return AttributeValueKind.TableSortMode;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:table:1.0" &&
            node.LocalName == "condition-source")
        {
            return AttributeValueKind.TableConditionSource;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:table:1.0" &&
            node.LocalName == "function")
        {
            return AttributeValueKind.TableFunction;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:database:1.0" &&
            (node.LocalName == "delete-rule" || node.LocalName == "update-rule"))
        {
            return AttributeValueKind.DatabaseRule;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:database:1.0" &&
            node.LocalName == "is-nullable")
        {
            return AttributeValueKind.DatabaseIsNullable;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:database:1.0" &&
            node.LocalName == "data-source-setting-type")
        {
            return AttributeValueKind.DatabaseDataSourceSettingType;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:animation:1.0" &&
            node.LocalName == "color-interpolation")
        {
            return AttributeValueKind.AnimationColorInterpolation;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:animation:1.0" &&
            node.LocalName == "color-interpolation-direction")
        {
            return AttributeValueKind.AnimationColorInterpolationDirection;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0" &&
            node.LocalName == "nohref")
        {
            return AttributeValueKind.DrawNoHref;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0" &&
            node.LocalName == "preset-class")
        {
            return AttributeValueKind.PresentationPresetClass;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0" &&
            node.LocalName == "transliteration-style")
        {
            return AttributeValueKind.NumberTransliterationStyle;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:style:1.0" &&
            node.LocalName == "script-type")
        {
            return AttributeValueKind.StyleScriptType;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:style:1.0" &&
            node.LocalName == "text-emphasize")
        {
            return AttributeValueKind.StyleTextEmphasize;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0" &&
            node.LocalName == "stroke-linejoin")
        {
            return AttributeValueKind.DrawStrokeLineJoin;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0" &&
            node.LocalName == "stroke-linecap")
        {
            return AttributeValueKind.SvgStrokeLineCap;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" &&
            node.LocalName == "keep-together")
        {
            return AttributeValueKind.FoKeepTogether;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" &&
            node.LocalName == "wrap-option")
        {
            return AttributeValueKind.FoWrapOption;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0" &&
            node.LocalName == "projection")
        {
            return AttributeValueKind.Dr3dProjection;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0" &&
            node.LocalName == "shade-mode")
        {
            return AttributeValueKind.Dr3dShadeMode;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0" &&
            node.LocalName == "fill-rule")
        {
            return AttributeValueKind.SvgFillRule;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:table:1.0" &&
            node.LocalName == "border-model")
        {
            return AttributeValueKind.TableBorderModel;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:text:1.0" &&
            node.LocalName == "label-followed-by")
        {
            return AttributeValueKind.TextLabelFollowedBy;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:text:1.0" &&
            node.LocalName == "list-level-position-and-space-mode")
        {
            return AttributeValueKind.TextListLevelPositionMode;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:text:1.0" &&
            node.LocalName == "index-scope")
        {
            return AttributeValueKind.TextIndexScope;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:text:1.0" &&
            node.LocalName == "table-type")
        {
            return AttributeValueKind.TextTableType;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:text:1.0" &&
            node.LocalName == "anchor-type")
        {
            return AttributeValueKind.TextAnchorType;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:text:1.0" &&
            node.LocalName == "note-class")
        {
            return AttributeValueKind.TextNoteClass;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:text:1.0" &&
            node.LocalName == "select-page")
        {
            return AttributeValueKind.TextSelectPage;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:text:1.0" &&
            node.LocalName == "reference-format")
        {
            return AttributeValueKind.TextReferenceFormat;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:text:1.0" &&
            node.LocalName == "start-numbering-at")
        {
            return AttributeValueKind.TextStartNumberingAt;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:text:1.0" &&
            node.LocalName == "footnotes-position")
        {
            return AttributeValueKind.TextFootnotesPosition;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:text:1.0" &&
            node.LocalName == "caption-sequence-format")
        {
            return AttributeValueKind.TextCaptionSequenceFormat;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:text:1.0" &&
            node.LocalName == "number-position")
        {
            return AttributeValueKind.TextNumberPosition;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:text:1.0" &&
            node.LocalName == "placeholder-type")
        {
            return AttributeValueKind.TextPlaceholderType;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:text:1.0" &&
            node.LocalName == "animation")
        {
            return AttributeValueKind.TextAnimation;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:text:1.0" &&
            node.LocalName == "animation-direction")
        {
            return AttributeValueKind.TextAnimationDirection;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:text:1.0" &&
            node.LocalName == "kind")
        {
            return AttributeValueKind.TextKind;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:style:1.0" &&
            node.LocalName == "font-relief")
        {
            return AttributeValueKind.FontRelief;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0" &&
            node.LocalName == "font-stretch")
        {
            return AttributeValueKind.FontStretch;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:style:1.0" &&
            node.LocalName == "line-break")
        {
            return AttributeValueKind.StyleLineBreak;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:style:1.0" &&
            node.LocalName == "repeat")
        {
            return AttributeValueKind.StyleRepeat;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:style:1.0" &&
            node.LocalName == "direction")
        {
            return AttributeValueKind.StyleDirection;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:form:1.0" &&
            node.LocalName == "orientation")
        {
            return AttributeValueKind.FormOrientation;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:table:1.0" &&
            node.LocalName == "direction")
        {
            return AttributeValueKind.TableDirection;
        }

        if (node.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:table:1.0" &&
            node.LocalName == "orientation")
        {
            return AttributeValueKind.TableOrientation;
        }

        AttributeValueKind valueKind = AttributeValueKind.Unknown;
        foreach (var child in node.Children)
        {
            valueKind = MergeValueKinds(valueKind, InferValueKind(child, metadata, visitedRefs));
        }

        return valueKind == AttributeValueKind.Unknown ? AttributeValueKind.String : valueKind;
    }

    private static AttributeValueKind InferValueKind(SchemaPatternNodeMetadata node, SchemaMetadata metadata, HashSet<string> visitedRefs)
    {
        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "borderWidths", StringComparison.Ordinal))
        {
            return AttributeValueKind.BorderWidths;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            IsLengthReference(node.ReferenceName))
        {
            return AttributeValueKind.Length;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "angle", StringComparison.Ordinal))
        {
            return AttributeValueKind.Angle;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "boolean", StringComparison.Ordinal))
        {
            return AttributeValueKind.Boolean;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            IsStyleNameReference(node.ReferenceName))
        {
            return AttributeValueKind.StyleName;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "styleNameRefs", StringComparison.Ordinal))
        {
            return AttributeValueKind.StyleNameList;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "color", StringComparison.Ordinal))
        {
            return AttributeValueKind.Color;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "anyIRI", StringComparison.Ordinal))
        {
            return AttributeValueKind.IriReference;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "zeroToHundredPercent", StringComparison.Ordinal))
        {
            return AttributeValueKind.Percent;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "signedZeroToHundredPercent", StringComparison.Ordinal))
        {
            return AttributeValueKind.SignedPercent;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "cellAddress", StringComparison.Ordinal))
        {
            return AttributeValueKind.CellAddress;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "cellRangeAddress", StringComparison.Ordinal))
        {
            return AttributeValueKind.CellRangeAddress;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "cellRangeAddressList", StringComparison.Ordinal))
        {
            return AttributeValueKind.CellRangeAddressList;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "vector3D", StringComparison.Ordinal))
        {
            return AttributeValueKind.Vector3D;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "point3D", StringComparison.Ordinal))
        {
            return AttributeValueKind.Point3D;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "points", StringComparison.Ordinal))
        {
            return AttributeValueKind.PointList;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "languageCode", StringComparison.Ordinal))
        {
            return AttributeValueKind.LanguageCode;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "countryCode", StringComparison.Ordinal))
        {
            return AttributeValueKind.CountryCode;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "scriptCode", StringComparison.Ordinal))
        {
            return AttributeValueKind.ScriptCode;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "language", StringComparison.Ordinal))
        {
            return AttributeValueKind.LanguageTag;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "namespacedToken", StringComparison.Ordinal))
        {
            return AttributeValueKind.NamespacedToken;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "character", StringComparison.Ordinal))
        {
            return AttributeValueKind.Character;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "textEncoding", StringComparison.Ordinal))
        {
            return AttributeValueKind.TextEncoding;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "targetFrameName", StringComparison.Ordinal))
        {
            return AttributeValueKind.TargetFrameName;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "lineStyle", StringComparison.Ordinal))
        {
            return AttributeValueKind.LineStyle;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "lineType", StringComparison.Ordinal))
        {
            return AttributeValueKind.LineType;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "lineWidth", StringComparison.Ordinal))
        {
            return AttributeValueKind.LineWidth;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "lineMode", StringComparison.Ordinal))
        {
            return AttributeValueKind.LineMode;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "fontStyle", StringComparison.Ordinal))
        {
            return AttributeValueKind.FontStyle;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "fontVariant", StringComparison.Ordinal))
        {
            return AttributeValueKind.FontVariant;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "fontWeight", StringComparison.Ordinal))
        {
            return AttributeValueKind.FontWeight;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "fontFamilyGeneric", StringComparison.Ordinal))
        {
            return AttributeValueKind.FontFamilyGeneric;
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            string.Equals(node.ReferenceName, "fontPitch", StringComparison.Ordinal))
        {
            return AttributeValueKind.FontPitch;
        }

        if (node.Kind == "data" || node.Kind == "value")
        {
            return GetValueKind(node.DataType);
        }

        if ((node.Kind == "ref" || node.Kind == "parentRef") &&
            !string.IsNullOrEmpty(node.ReferenceName) &&
            visitedRefs.Add(node.ReferenceName))
        {
            var refPattern = metadata.Patterns.FirstOrDefault(p => p.Name == node.ReferenceName);
            if (refPattern is not null)
            {
                AttributeValueKind valueKind = AttributeValueKind.Unknown;
                foreach (var refNode in refPattern.PatternTree)
                {
                    valueKind = MergeValueKinds(valueKind, InferValueKind(refNode, metadata, visitedRefs));
                }

                return valueKind;
            }
        }

        AttributeValueKind childValueKind = AttributeValueKind.Unknown;
        foreach (var child in node.Children)
        {
            childValueKind = MergeValueKinds(childValueKind, InferValueKind(child, metadata, visitedRefs));
        }

        return childValueKind;
    }

    private static AttributeValueKind GetValueKind(string dataType)
    {
        string normalized = dataType.Replace("-", string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "boolean" => AttributeValueKind.Boolean,
            "byte" or "short" or "int" or "integer" or "long" or "nonnegativeinteger" or "positiveinteger" or "nonpositiveinteger" or "negativeinteger" => AttributeValueKind.Int32,
            "decimal" or "double" or "float" => AttributeValueKind.Decimal,
            "date" or "datetime" => AttributeValueKind.DateTime,
            "time" => AttributeValueKind.Time,
            "duration" => AttributeValueKind.Duration,
            "ncname" or "id" or "idref" => AttributeValueKind.XmlName,
            "string" or "normalizedstring" or "token" or "language" or "name" or "anyuri" => AttributeValueKind.String,
            _ => AttributeValueKind.String
        };
    }

    private static AttributeValueKind MergeValueKinds(AttributeValueKind first, AttributeValueKind second)
    {
        if (first == AttributeValueKind.Unknown)
        {
            return second;
        }

        if (second == AttributeValueKind.Unknown || first == second)
        {
            return first;
        }

        return AttributeValueKind.String;
    }

    private static void WriteAttributeProperty(TextWriter writer, AttributePropertyMetadata attr, string propName, string prefix)
    {
        writer.WriteLine();
        switch (attr.ValueKind)
        {
            case AttributeValueKind.Int32:
                writer.WriteLine($"        public int? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetNullableInt32AttributeValue",
                    "SetInt32AttributeValue");
                break;
            case AttributeValueKind.Boolean:
                writer.WriteLine($"        public bool? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetBooleanAttributeValue",
                    "SetBooleanAttributeValue");
                break;
            case AttributeValueKind.Decimal:
                writer.WriteLine($"        public decimal? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetDecimalAttributeValue",
                    "SetDecimalAttributeValue");
                break;
            case AttributeValueKind.DateTime:
                writer.WriteLine($"        public DateTime? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetDateTimeAttributeValue",
                    "SetDateTimeAttributeValue");
                break;
            case AttributeValueKind.Time:
                writer.WriteLine($"        public OdfTime? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTimeAttributeValue",
                    "SetTimeAttributeValue");
                break;
            case AttributeValueKind.Length:
                writer.WriteLine($"        public OdfLength? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetLengthAttributeValue",
                    "SetLengthAttributeValue");
                break;
            case AttributeValueKind.BorderWidths:
                writer.WriteLine($"        public OdfBorderWidths? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetBorderWidthsAttributeValue",
                    "SetBorderWidthsAttributeValue");
                break;
            case AttributeValueKind.Duration:
                writer.WriteLine($"        public OdfDuration? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetDurationAttributeValue",
                    "SetDurationAttributeValue");
                break;
            case AttributeValueKind.Angle:
                writer.WriteLine($"        public OdfAngle? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetAngleAttributeValue",
                    "SetAngleAttributeValue");
                break;
            case AttributeValueKind.StyleName:
                writer.WriteLine($"        public OdfStyleName? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleNameAttributeValue",
                    "SetStyleNameAttributeValue");
                break;
            case AttributeValueKind.StyleNameList:
                writer.WriteLine($"        public OdfStyleNameList? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleNameListAttributeValue",
                    "SetStyleNameListAttributeValue");
                break;
            case AttributeValueKind.Color:
                writer.WriteLine($"        public OdfColor? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetColorAttributeValue",
                    "SetColorAttributeValue");
                break;
            case AttributeValueKind.IriReference:
                writer.WriteLine($"        public OdfIriReference? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetIriReferenceAttributeValue",
                    "SetIriReferenceAttributeValue");
                break;
            case AttributeValueKind.XLinkType:
                writer.WriteLine($"        public OdfXLinkType? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetXLinkTypeAttributeValue",
                    "SetXLinkTypeAttributeValue");
                break;
            case AttributeValueKind.XLinkShow:
                writer.WriteLine($"        public OdfXLinkShow? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetXLinkShowAttributeValue",
                    "SetXLinkShowAttributeValue");
                break;
            case AttributeValueKind.XLinkActuate:
                writer.WriteLine($"        public OdfXLinkActuate? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetXLinkActuateAttributeValue",
                    "SetXLinkActuateAttributeValue");
                break;
            case AttributeValueKind.NumberStyle:
                writer.WriteLine($"        public OdfNumberStyle? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetNumberStyleAttributeValue",
                    "SetNumberStyleAttributeValue");
                break;
            case AttributeValueKind.NumberCalendar:
                writer.WriteLine($"        public OdfNumberCalendar? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetNumberCalendarAttributeValue",
                    "SetNumberCalendarAttributeValue");
                break;
            case AttributeValueKind.TableOrder:
                writer.WriteLine($"        public OdfTableOrder? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTableOrderAttributeValue",
                    "SetTableOrderAttributeValue");
                break;
            case AttributeValueKind.TableType:
                writer.WriteLine($"        public OdfTableType? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTableTypeAttributeValue",
                    "SetTableTypeAttributeValue");
                break;
            case AttributeValueKind.PresentationEffect:
                writer.WriteLine($"        public OdfPresentationEffect? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetPresentationEffectAttributeValue",
                    "SetPresentationEffectAttributeValue");
                break;
            case AttributeValueKind.PresentationSpeed:
                writer.WriteLine($"        public OdfPresentationSpeed? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetPresentationSpeedAttributeValue",
                    "SetPresentationSpeedAttributeValue");
                break;
            case AttributeValueKind.PresentationAction:
                writer.WriteLine($"        public OdfPresentationAction? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetPresentationActionAttributeValue",
                    "SetPresentationActionAttributeValue");
                break;
            case AttributeValueKind.PresentationTransitionType:
                writer.WriteLine($"        public OdfPresentationTransitionType? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetPresentationTransitionTypeAttributeValue",
                    "SetPresentationTransitionTypeAttributeValue");
                break;
            case AttributeValueKind.PresentationTransitionStyle:
                writer.WriteLine($"        public OdfPresentationTransitionStyle? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetPresentationTransitionStyleAttributeValue",
                    "SetPresentationTransitionStyleAttributeValue");
                break;
            case AttributeValueKind.FoTextTransform:
                writer.WriteLine($"        public OdfFoTextTransform? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetFoTextTransformAttributeValue",
                    "SetFoTextTransformAttributeValue");
                break;
            case AttributeValueKind.FoTextAlign:
                writer.WriteLine($"        public OdfFoTextAlign? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetFoTextAlignAttributeValue",
                    "SetFoTextAlignAttributeValue");
                break;
            case AttributeValueKind.StyleTextRotationScale:
                writer.WriteLine($"        public OdfStyleTextRotationScale? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleTextRotationScaleAttributeValue",
                    "SetStyleTextRotationScaleAttributeValue");
                break;
            case AttributeValueKind.StyleTextCombine:
                writer.WriteLine($"        public OdfStyleTextCombine? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleTextCombineAttributeValue",
                    "SetStyleTextCombineAttributeValue");
                break;
            case AttributeValueKind.DrawFill:
                writer.WriteLine($"        public OdfDrawFill? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetDrawFillAttributeValue",
                    "SetDrawFillAttributeValue");
                break;
            case AttributeValueKind.SmilFill:
                writer.WriteLine($"        public OdfSmilFill? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetSmilFillAttributeValue",
                    "SetSmilFillAttributeValue");
                break;
            case AttributeValueKind.DrawFillImageRefPoint:
                writer.WriteLine($"        public OdfDrawFillImageRefPoint? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetDrawFillImageRefPointAttributeValue",
                    "SetDrawFillImageRefPointAttributeValue");
                break;
            case AttributeValueKind.DrawColorMode:
                writer.WriteLine($"        public OdfDrawColorMode? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetDrawColorModeAttributeValue",
                    "SetDrawColorModeAttributeValue");
                break;
            case AttributeValueKind.StyleVerticalAlign:
                writer.WriteLine($"        public OdfStyleVerticalAlign? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleVerticalAlignAttributeValue",
                    "SetStyleVerticalAlignAttributeValue");
                break;
            case AttributeValueKind.StyleVerticalPos:
                writer.WriteLine($"        public OdfStyleVerticalPos? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleVerticalPosAttributeValue",
                    "SetStyleVerticalPosAttributeValue");
                break;
            case AttributeValueKind.StyleVerticalRel:
                writer.WriteLine($"        public OdfStyleVerticalRel? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleVerticalRelAttributeValue",
                    "SetStyleVerticalRelAttributeValue");
                break;
            case AttributeValueKind.StyleHorizontalPos:
                writer.WriteLine($"        public OdfStyleHorizontalPos? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleHorizontalPosAttributeValue",
                    "SetStyleHorizontalPosAttributeValue");
                break;
            case AttributeValueKind.StyleHorizontalRel:
                writer.WriteLine($"        public OdfStyleHorizontalRel? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleHorizontalRelAttributeValue",
                    "SetStyleHorizontalRelAttributeValue");
                break;
            case AttributeValueKind.StyleWrap:
                writer.WriteLine($"        public OdfStyleWrap? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleWrapAttributeValue",
                    "SetStyleWrapAttributeValue");
                break;
            case AttributeValueKind.StyleRunThrough:
                writer.WriteLine($"        public OdfStyleRunThrough? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleRunThroughAttributeValue",
                    "SetStyleRunThroughAttributeValue");
                break;
            case AttributeValueKind.StyleWrapContourMode:
                writer.WriteLine($"        public OdfStyleWrapContourMode? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleWrapContourModeAttributeValue",
                    "SetStyleWrapContourModeAttributeValue");
                break;
            case AttributeValueKind.StyleWritingMode:
                writer.WriteLine($"        public OdfStyleWritingMode? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleWritingModeAttributeValue",
                    "SetStyleWritingModeAttributeValue");
                break;
            case AttributeValueKind.TableDisplayMemberMode:
                writer.WriteLine($"        public OdfTableDisplayMemberMode? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTableDisplayMemberModeAttributeValue",
                    "SetTableDisplayMemberModeAttributeValue");
                break;
            case AttributeValueKind.TableLayoutMode:
                writer.WriteLine($"        public OdfTableLayoutMode? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTableLayoutModeAttributeValue",
                    "SetTableLayoutModeAttributeValue");
                break;
            case AttributeValueKind.TableMemberType:
                writer.WriteLine($"        public OdfTableMemberType? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTableMemberTypeAttributeValue",
                    "SetTableMemberTypeAttributeValue");
                break;
            case AttributeValueKind.TableGroupedBy:
                writer.WriteLine($"        public OdfTableGroupedBy? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTableGroupedByAttributeValue",
                    "SetTableGroupedByAttributeValue");
                break;
            case AttributeValueKind.TableSortMode:
                writer.WriteLine($"        public OdfTableSortMode? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTableSortModeAttributeValue",
                    "SetTableSortModeAttributeValue");
                break;
            case AttributeValueKind.TableConditionSource:
                writer.WriteLine($"        public OdfTableConditionSource? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTableConditionSourceAttributeValue",
                    "SetTableConditionSourceAttributeValue");
                break;
            case AttributeValueKind.TableFunction:
                writer.WriteLine($"        public OdfTableFunction? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTableFunctionAttributeValue",
                    "SetTableFunctionAttributeValue");
                break;
            case AttributeValueKind.DatabaseRule:
                writer.WriteLine($"        public OdfDatabaseRule? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetDatabaseRuleAttributeValue",
                    "SetDatabaseRuleAttributeValue");
                break;
            case AttributeValueKind.DatabaseIsNullable:
                writer.WriteLine($"        public OdfDatabaseIsNullable? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetDatabaseIsNullableAttributeValue",
                    "SetDatabaseIsNullableAttributeValue");
                break;
            case AttributeValueKind.DatabaseDataSourceSettingType:
                writer.WriteLine($"        public OdfDatabaseDataSourceSettingType? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetDatabaseDataSourceSettingTypeAttributeValue",
                    "SetDatabaseDataSourceSettingTypeAttributeValue");
                break;
            case AttributeValueKind.AnimationColorInterpolation:
                writer.WriteLine($"        public OdfAnimationColorInterpolation? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetAnimationColorInterpolationAttributeValue",
                    "SetAnimationColorInterpolationAttributeValue");
                break;
            case AttributeValueKind.AnimationColorInterpolationDirection:
                writer.WriteLine($"        public OdfAnimationColorInterpolationDirection? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetAnimationColorInterpolationDirectionAttributeValue",
                    "SetAnimationColorInterpolationDirectionAttributeValue");
                break;
            case AttributeValueKind.DrawNoHref:
                writer.WriteLine($"        public OdfDrawNoHref? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetDrawNoHrefAttributeValue",
                    "SetDrawNoHrefAttributeValue");
                break;
            case AttributeValueKind.PresentationPresetClass:
                writer.WriteLine($"        public OdfPresentationPresetClass? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetPresentationPresetClassAttributeValue",
                    "SetPresentationPresetClassAttributeValue");
                break;
            case AttributeValueKind.NumberTransliterationStyle:
                writer.WriteLine($"        public OdfNumberTransliterationStyle? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetNumberTransliterationStyleAttributeValue",
                    "SetNumberTransliterationStyleAttributeValue");
                break;
            case AttributeValueKind.StyleScriptType:
                writer.WriteLine($"        public OdfStyleScriptType? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleScriptTypeAttributeValue",
                    "SetStyleScriptTypeAttributeValue");
                break;
            case AttributeValueKind.StyleTextEmphasize:
                writer.WriteLine($"        public OdfStyleTextEmphasize? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleTextEmphasizeAttributeValue",
                    "SetStyleTextEmphasizeAttributeValue");
                break;
            case AttributeValueKind.DrawStrokeLineJoin:
                writer.WriteLine($"        public OdfDrawStrokeLineJoin? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetDrawStrokeLineJoinAttributeValue",
                    "SetDrawStrokeLineJoinAttributeValue");
                break;
            case AttributeValueKind.SvgStrokeLineCap:
                writer.WriteLine($"        public OdfSvgStrokeLineCap? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetSvgStrokeLineCapAttributeValue",
                    "SetSvgStrokeLineCapAttributeValue");
                break;
            case AttributeValueKind.FoKeepTogether:
                writer.WriteLine($"        public OdfFoKeepTogether? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetFoKeepTogetherAttributeValue",
                    "SetFoKeepTogetherAttributeValue");
                break;
            case AttributeValueKind.FoWrapOption:
                writer.WriteLine($"        public OdfFoWrapOption? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetFoWrapOptionAttributeValue",
                    "SetFoWrapOptionAttributeValue");
                break;
            case AttributeValueKind.Dr3dProjection:
                writer.WriteLine($"        public OdfDr3dProjection? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetDr3dProjectionAttributeValue",
                    "SetDr3dProjectionAttributeValue");
                break;
            case AttributeValueKind.Dr3dShadeMode:
                writer.WriteLine($"        public OdfDr3dShadeMode? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetDr3dShadeModeAttributeValue",
                    "SetDr3dShadeModeAttributeValue");
                break;
            case AttributeValueKind.SvgFillRule:
                writer.WriteLine($"        public OdfSvgFillRule? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetSvgFillRuleAttributeValue",
                    "SetSvgFillRuleAttributeValue");
                break;
            case AttributeValueKind.TableBorderModel:
                writer.WriteLine($"        public OdfTableBorderModel? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTableBorderModelAttributeValue",
                    "SetTableBorderModelAttributeValue");
                break;
            case AttributeValueKind.TextLabelFollowedBy:
                writer.WriteLine($"        public OdfTextLabelFollowedBy? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTextLabelFollowedByAttributeValue",
                    "SetTextLabelFollowedByAttributeValue");
                break;
            case AttributeValueKind.TextListLevelPositionMode:
                writer.WriteLine($"        public OdfTextListLevelPositionMode? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTextListLevelPositionModeAttributeValue",
                    "SetTextListLevelPositionModeAttributeValue");
                break;
            case AttributeValueKind.TextIndexScope:
                writer.WriteLine($"        public OdfTextIndexScope? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTextIndexScopeAttributeValue",
                    "SetTextIndexScopeAttributeValue");
                break;
            case AttributeValueKind.TextTableType:
                writer.WriteLine($"        public OdfTextTableType? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTextTableTypeAttributeValue",
                    "SetTextTableTypeAttributeValue");
                break;
            case AttributeValueKind.TextAnchorType:
                writer.WriteLine($"        public OdfTextAnchorType? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTextAnchorTypeAttributeValue",
                    "SetTextAnchorTypeAttributeValue");
                break;
            case AttributeValueKind.TextNoteClass:
                writer.WriteLine($"        public OdfTextNoteClass? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTextNoteClassAttributeValue",
                    "SetTextNoteClassAttributeValue");
                break;
            case AttributeValueKind.TextSelectPage:
                writer.WriteLine($"        public OdfTextSelectPage? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTextSelectPageAttributeValue",
                    "SetTextSelectPageAttributeValue");
                break;
            case AttributeValueKind.TextReferenceFormat:
                writer.WriteLine($"        public OdfTextReferenceFormat? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTextReferenceFormatAttributeValue",
                    "SetTextReferenceFormatAttributeValue");
                break;
            case AttributeValueKind.TextStartNumberingAt:
                writer.WriteLine($"        public OdfTextStartNumberingAt? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTextStartNumberingAtAttributeValue",
                    "SetTextStartNumberingAtAttributeValue");
                break;
            case AttributeValueKind.TextFootnotesPosition:
                writer.WriteLine($"        public OdfTextFootnotesPosition? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTextFootnotesPositionAttributeValue",
                    "SetTextFootnotesPositionAttributeValue");
                break;
            case AttributeValueKind.TextCaptionSequenceFormat:
                writer.WriteLine($"        public OdfTextCaptionSequenceFormat? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTextCaptionSequenceFormatAttributeValue",
                    "SetTextCaptionSequenceFormatAttributeValue");
                break;
            case AttributeValueKind.TextNumberPosition:
                writer.WriteLine($"        public OdfTextNumberPosition? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTextNumberPositionAttributeValue",
                    "SetTextNumberPositionAttributeValue");
                break;
            case AttributeValueKind.TextPlaceholderType:
                writer.WriteLine($"        public OdfTextPlaceholderType? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTextPlaceholderTypeAttributeValue",
                    "SetTextPlaceholderTypeAttributeValue");
                break;
            case AttributeValueKind.TextAnimation:
                writer.WriteLine($"        public OdfTextAnimation? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTextAnimationAttributeValue",
                    "SetTextAnimationAttributeValue");
                break;
            case AttributeValueKind.TextAnimationDirection:
                writer.WriteLine($"        public OdfTextAnimationDirection? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTextAnimationDirectionAttributeValue",
                    "SetTextAnimationDirectionAttributeValue");
                break;
            case AttributeValueKind.TextKind:
                writer.WriteLine($"        public OdfTextKind? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTextKindAttributeValue",
                    "SetTextKindAttributeValue");
                break;
            case AttributeValueKind.Percent:
                writer.WriteLine($"        public OdfPercent? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetPercentAttributeValue",
                    "SetPercentAttributeValue");
                break;
            case AttributeValueKind.SignedPercent:
                writer.WriteLine($"        public OdfPercent? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetSignedPercentAttributeValue",
                    "SetSignedPercentAttributeValue");
                break;
            case AttributeValueKind.CellAddress:
                writer.WriteLine($"        public OdfCellAddressReference? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetCellAddressAttributeValue",
                    "SetCellAddressAttributeValue");
                break;
            case AttributeValueKind.CellRangeAddress:
                writer.WriteLine($"        public OdfCellRangeAddress? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetCellRangeAddressAttributeValue",
                    "SetCellRangeAddressAttributeValue");
                break;
            case AttributeValueKind.CellRangeAddressList:
                writer.WriteLine($"        public OdfCellRangeAddressList? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetCellRangeAddressListAttributeValue",
                    "SetCellRangeAddressListAttributeValue");
                break;
            case AttributeValueKind.Vector3D:
                writer.WriteLine($"        public OdfVector3D? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetVector3DAttributeValue",
                    "SetVector3DAttributeValue");
                break;
            case AttributeValueKind.Point3D:
                writer.WriteLine($"        public OdfPoint3D? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetPoint3DAttributeValue",
                    "SetPoint3DAttributeValue");
                break;
            case AttributeValueKind.PointList:
                writer.WriteLine($"        public OdfPointList? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetPointListAttributeValue",
                    "SetPointListAttributeValue");
                break;
            case AttributeValueKind.LanguageCode:
                writer.WriteLine($"        public OdfLanguageCode? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetLanguageCodeAttributeValue",
                    "SetLanguageCodeAttributeValue");
                break;
            case AttributeValueKind.CountryCode:
                writer.WriteLine($"        public OdfCountryCode? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetCountryCodeAttributeValue",
                    "SetCountryCodeAttributeValue");
                break;
            case AttributeValueKind.ScriptCode:
                writer.WriteLine($"        public OdfScriptCode? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetScriptCodeAttributeValue",
                    "SetScriptCodeAttributeValue");
                break;
            case AttributeValueKind.LanguageTag:
                writer.WriteLine($"        public OdfLanguageTag? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetLanguageTagAttributeValue",
                    "SetLanguageTagAttributeValue");
                break;
            case AttributeValueKind.NamespacedToken:
                writer.WriteLine($"        public OdfNamespacedToken? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetNamespacedTokenAttributeValue",
                    "SetNamespacedTokenAttributeValue");
                break;
            case AttributeValueKind.Character:
                writer.WriteLine($"        public OdfCharacter? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetCharacterAttributeValue",
                    "SetCharacterAttributeValue");
                break;
            case AttributeValueKind.TextEncoding:
                writer.WriteLine($"        public OdfTextEncoding? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTextEncodingAttributeValue",
                    "SetTextEncodingAttributeValue");
                break;
            case AttributeValueKind.TargetFrameName:
                writer.WriteLine($"        public OdfTargetFrameName? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTargetFrameNameAttributeValue",
                    "SetTargetFrameNameAttributeValue");
                break;
            case AttributeValueKind.LineStyle:
                writer.WriteLine($"        public OdfLineStyle? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetLineStyleAttributeValue",
                    "SetLineStyleAttributeValue");
                break;
            case AttributeValueKind.LineType:
                writer.WriteLine($"        public OdfLineType? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetLineTypeAttributeValue",
                    "SetLineTypeAttributeValue");
                break;
            case AttributeValueKind.LineWidth:
                writer.WriteLine($"        public OdfLineWidth? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetLineWidthAttributeValue",
                    "SetLineWidthAttributeValue");
                break;
            case AttributeValueKind.LineMode:
                writer.WriteLine($"        public OdfLineMode? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetLineModeAttributeValue",
                    "SetLineModeAttributeValue");
                break;
            case AttributeValueKind.FontStyle:
                writer.WriteLine($"        public OdfFontStyle? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetFontStyleAttributeValue",
                    "SetFontStyleAttributeValue");
                break;
            case AttributeValueKind.FontVariant:
                writer.WriteLine($"        public OdfFontVariant? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetFontVariantAttributeValue",
                    "SetFontVariantAttributeValue");
                break;
            case AttributeValueKind.FontWeight:
                writer.WriteLine($"        public OdfFontWeight? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetFontWeightAttributeValue",
                    "SetFontWeightAttributeValue");
                break;
            case AttributeValueKind.FontFamilyGeneric:
                writer.WriteLine($"        public OdfFontFamilyGeneric? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetFontFamilyGenericAttributeValue",
                    "SetFontFamilyGenericAttributeValue");
                break;
            case AttributeValueKind.FontPitch:
                writer.WriteLine($"        public OdfFontPitch? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetFontPitchAttributeValue",
                    "SetFontPitchAttributeValue");
                break;
            case AttributeValueKind.FontRelief:
                writer.WriteLine($"        public OdfFontRelief? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetFontReliefAttributeValue",
                    "SetFontReliefAttributeValue");
                break;
            case AttributeValueKind.FontStretch:
                writer.WriteLine($"        public OdfFontStretch? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetFontStretchAttributeValue",
                    "SetFontStretchAttributeValue");
                break;
            case AttributeValueKind.StyleLineBreak:
                writer.WriteLine($"        public OdfStyleLineBreak? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleLineBreakAttributeValue",
                    "SetStyleLineBreakAttributeValue");
                break;
            case AttributeValueKind.StyleRepeat:
                writer.WriteLine($"        public OdfStyleRepeat? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleRepeatAttributeValue",
                    "SetStyleRepeatAttributeValue");
                break;
            case AttributeValueKind.StyleDirection:
                writer.WriteLine($"        public OdfStyleDirection? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleDirectionAttributeValue",
                    "SetStyleDirectionAttributeValue");
                break;
            case AttributeValueKind.FormOrientation:
                writer.WriteLine($"        public OdfFormOrientation? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetFormOrientationAttributeValue",
                    "SetFormOrientationAttributeValue");
                break;
            case AttributeValueKind.TableDirection:
                writer.WriteLine($"        public OdfTableDirection? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTableDirectionAttributeValue",
                    "SetTableDirectionAttributeValue");
                break;
            case AttributeValueKind.TableOrientation:
                writer.WriteLine($"        public OdfTableOrientation? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetTableOrientationAttributeValue",
                    "SetTableOrientationAttributeValue");
                break;
            case AttributeValueKind.XmlName:
                writer.WriteLine($"        public OdfXmlName? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetXmlNameAttributeValue",
                    "SetXmlNameAttributeValue");
                break;
            case AttributeValueKind.StyleFamily:
                writer.WriteLine($"        public OdfStyleFamily? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetStyleFamilyAttributeValue",
                    "SetStyleFamilyAttributeValue");
                break;
            case AttributeValueKind.OdfVersion:
                writer.WriteLine($"        public OdfVersion? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetOdfVersionAttributeValue",
                    "SetOdfVersionAttributeValue");
                break;
            case AttributeValueKind.MediaType:
                writer.WriteLine($"        public OdfMediaType? {propName}");
                WriteNullableTypedAttributePropertyBody(
                    writer,
                    attr,
                    prefix,
                    "GetMediaTypeAttributeValue",
                    "SetMediaTypeAttributeValue");
                break;
            default:
                writer.WriteLine($"        public string? {propName}");
                writer.WriteLine("        {");
                writer.WriteLine($"            get => GetAttributeValue(\"{attr.LocalName}\", \"{attr.NamespaceUri}\", GetDocumentVersion());");
                writer.WriteLine("            set");
                writer.WriteLine("            {");
                writer.WriteLine("                if (value == null)");
                writer.WriteLine($"                    RemoveAttribute(\"{attr.LocalName}\", \"{attr.NamespaceUri}\");");
                writer.WriteLine("                else");
                writer.WriteLine($"                    SetAttributeValue(\"{attr.LocalName}\", \"{attr.NamespaceUri}\", value, \"{prefix}\", GetDocumentVersion());");
                writer.WriteLine("            }");
                writer.WriteLine("        }");
                break;
        }
    }

    private static void WriteNullableTypedAttributePropertyBody(
        TextWriter writer,
        AttributePropertyMetadata attr,
        string prefix,
        string getterName,
        string setterName)
    {
        writer.WriteLine("        {");
        writer.WriteLine($"            get => {getterName}(\"{attr.LocalName}\", \"{attr.NamespaceUri}\", GetDocumentVersion());");
        writer.WriteLine("            set");
        writer.WriteLine("            {");
        writer.WriteLine("                if (value == null)");
        writer.WriteLine($"                    RemoveAttribute(\"{attr.LocalName}\", \"{attr.NamespaceUri}\");");
        writer.WriteLine("                else");
        writer.WriteLine($"                    {setterName}(\"{attr.LocalName}\", \"{attr.NamespaceUri}\", value.Value, \"{prefix}\", GetDocumentVersion());");
        writer.WriteLine("            }");
        writer.WriteLine("        }");
    }

    private static string ToPascalCase(string s)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;
        var parts = s.Split(new[] { '-', '_', ':', '.' }, StringSplitOptions.RemoveEmptyEntries);
        var pascalParts = parts.Select(p =>
        {
            if (p.Length == 0)
                return string.Empty;
            return char.ToUpper(p[0]) + p.Substring(1);
        });
        return string.Join("", pascalParts);
    }

    private static bool IsLengthReference(string? referenceName)
    {
        return referenceName is
            "length" or
            "nonNegativeLength" or
            "positiveLength" or
            "nonNegativePixelLength" or
            "percent" or
            "nonNegativePercent";
    }

    private static bool IsStyleNameReference(string? referenceName)
    {
        return referenceName is "styleName" or "styleNameRef";
    }

    private static string? GetNamespacePrefix(string namespaceUri)
    {
        return namespaceUri switch
        {
            "urn:oasis:names:tc:opendocument:xmlns:office:1.0" => "Office",
            "urn:oasis:names:tc:opendocument:xmlns:style:1.0" => "Style",
            "urn:oasis:names:tc:opendocument:xmlns:text:1.0" => "Text",
            "urn:oasis:names:tc:opendocument:xmlns:table:1.0" => "Table",
            "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0" => "Draw",
            "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" => "Fo",
            "http://www.w3.org/1999/xlink" => "XLink",
            "http://purl.org/dc/elements/1.1/" => "Dc",
            "urn:oasis:names:tc:opendocument:xmlns:meta:1.0" => "Meta",
            "urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0" => "Number",
            "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0" => "Presentation",
            "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0" => "Smil",
            "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0" => "Svg",
            "urn:oasis:names:tc:opendocument:xmlns:chart:1.0" => "Chart",
            "urn:oasis:names:tc:opendocument:xmlns:config:1.0" => "Config",
            "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0" => "Manifest",
            "urn:oasis:names:tc:opendocument:xmlns:digitalsignature:1.0" => "Dsig",
            "http://www.w3.org/2000/09/xmldsig#" => "Ds",
            _ => null
        };
    }

    private static string GetPrefix(string namespaceUri)
    {
        string? p = GetNamespacePrefix(namespaceUri);
        return p != null ? p.ToLowerInvariant() : "ns";
    }

    private static string GetNamespacePascalName(string namespaceUri)
    {
        string? prefix = GetNamespacePrefix(namespaceUri);
        if (prefix != null)
            return prefix;

        string segment = namespaceUri;
        if (namespaceUri.StartsWith("urn:", StringComparison.Ordinal))
        {
            var parts = namespaceUri.Split(':');
            if (parts.Length > 0)
            {
                segment = parts[parts.Length - 1];
                if (segment == "1.0" || segment == "2.0" || segment == "3.0")
                {
                    if (parts.Length > 1)
                        segment = parts[parts.Length - 2];
                }
            }
        }
        else
        {
            var parts = namespaceUri.Split('/');
            if (parts.Length > 0)
            {
                segment = parts[parts.Length - 1];
                if (string.IsNullOrEmpty(segment) && parts.Length > 1)
                {
                    segment = parts[parts.Length - 2];
                }
            }
        }
        return ToPascalCase(segment);
    }

    private static string GetElementClassName(string namespaceUri, string localName)
    {
        return GetNamespacePascalName(namespaceUri) + ToPascalCase(localName) + "Element";
    }

    private enum AttributeValueKind
    {
        Unknown,
        String,
        Int32,
        Boolean,
        Decimal,
        DateTime,
        Time,
        Length,
        BorderWidths,
        Duration,
        Angle,
        StyleName,
        StyleNameList,
        Color,
        IriReference,
        XLinkType,
        XLinkShow,
        XLinkActuate,
        NumberStyle,
        NumberCalendar,
        TableOrder,
        TableType,
        PresentationEffect,
        PresentationSpeed,
        PresentationAction,
        PresentationTransitionType,
        PresentationTransitionStyle,
        FoTextTransform,
        FoTextAlign,
        StyleTextRotationScale,
        StyleTextCombine,
        DrawFill,
        SmilFill,
        DrawFillImageRefPoint,
        DrawColorMode,
        StyleVerticalAlign,
        StyleVerticalPos,
        StyleVerticalRel,
        StyleHorizontalPos,
        StyleHorizontalRel,
        StyleWrap,
        StyleRunThrough,
        StyleWrapContourMode,
        StyleWritingMode,
        TableDisplayMemberMode,
        TableLayoutMode,
        TableMemberType,
        TableGroupedBy,
        TableSortMode,
        TableConditionSource,
        TableFunction,
        DatabaseRule,
        DatabaseIsNullable,
        DatabaseDataSourceSettingType,
        AnimationColorInterpolation,
        AnimationColorInterpolationDirection,
        DrawNoHref,
        PresentationPresetClass,
        NumberTransliterationStyle,
        StyleScriptType,
        StyleTextEmphasize,
        DrawStrokeLineJoin,
        SvgStrokeLineCap,
        FoKeepTogether,
        FoWrapOption,
        Dr3dProjection,
        Dr3dShadeMode,
        SvgFillRule,
        TableBorderModel,
        TextLabelFollowedBy,
        TextListLevelPositionMode,
        TextIndexScope,
        TextTableType,
        TextAnchorType,
        TextNoteClass,
        TextSelectPage,
        TextReferenceFormat,
        TextStartNumberingAt,
        TextFootnotesPosition,
        TextCaptionSequenceFormat,
        TextNumberPosition,
        TextPlaceholderType,
        TextAnimation,
        TextAnimationDirection,
        TextKind,
        Percent,
        SignedPercent,
        CellAddress,
        CellRangeAddress,
        CellRangeAddressList,
        Vector3D,
        Point3D,
        PointList,
        LanguageCode,
        CountryCode,
        ScriptCode,
        LanguageTag,
        NamespacedToken,
        Character,
        TextEncoding,
        TargetFrameName,
        LineStyle,
        LineType,
        LineWidth,
        LineMode,
        FontStyle,
        FontVariant,
        FontWeight,
        FontFamilyGeneric,
        FontPitch,
        FontRelief,
        FontStretch,
        StyleLineBreak,
        StyleRepeat,
        StyleDirection,
        FormOrientation,
        TableDirection,
        TableOrientation,
        XmlName,
        StyleFamily,
        OdfVersion,
        MediaType
    }

    private sealed class AttributePropertyMetadata
    {
        public string NamespaceUri { get; set; } = string.Empty;

        public string LocalName { get; set; } = string.Empty;

        public AttributeValueKind ValueKind { get; set; }
    }

    private sealed class ChildElementPropertyMetadata
    {
        public string NamespaceUri { get; set; } = string.Empty;

        public string LocalName { get; set; } = string.Empty;
    }
}
