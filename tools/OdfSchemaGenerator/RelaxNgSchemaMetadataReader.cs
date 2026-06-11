using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace OdfKit.Tools.OdfSchemaGenerator;

/// <summary>
/// Extracts deterministic ODF schema metadata from RELAX NG XML syntax.
/// </summary>
public sealed class RelaxNgSchemaMetadataReader
{
    private const string RelaxNgNamespace = "http://relaxng.org/ns/structure/1.0";

    /// <summary>
    /// Reads schema metadata from a RELAX NG file and its relative include graph.
    /// </summary>
    public SchemaMetadata ReadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Schema path cannot be empty.", nameof(path));

        string fullPath = Path.GetFullPath(path);
        string rootDirectory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        var state = new ReaderState(fullPath, rootDirectory);
        ReadFileInto(fullPath, state);
        return state.ToMetadata();
    }

    /// <summary>
    /// Reads schema metadata from a RELAX NG stream.
    /// </summary>
    public SchemaMetadata Read(Stream stream, string source)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        string baseDirectory = string.IsNullOrWhiteSpace(source)
            ? Directory.GetCurrentDirectory()
            : Path.GetDirectoryName(Path.GetFullPath(source)) ?? Directory.GetCurrentDirectory();
        var state = new ReaderState(source ?? string.Empty, baseDirectory);
        ReadStreamInto(stream, source ?? string.Empty, baseDirectory, state);
        return state.ToMetadata();
    }

    private static void ReadFileInto(string path, ReaderState state)
    {
        string fullPath = Path.GetFullPath(path);
        if (!state.VisitedFiles.Add(fullPath))
        {
            return;
        }

        using Stream stream = File.OpenRead(fullPath);
        string baseDirectory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        ReadStreamInto(stream, fullPath, baseDirectory, state);
    }

    private static void ReadStreamInto(Stream stream, string source, string baseDirectory, ReaderState state)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreWhitespace = true,
            MaxCharactersFromEntities = 0,
            CloseInput = false
        };

        using XmlReader reader = XmlReader.Create(stream, settings);
        var namespaceContextByDepth = new Dictionary<int, string?>();
        var datatypeLibraryContextByDepth = new Dictionary<int, string?>();
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.NamespaceURI != RelaxNgNamespace)
            {
                continue;
            }

            RemoveContextsBelow(namespaceContextByDepth, reader.Depth);
            RemoveContextsBelow(datatypeLibraryContextByDepth, reader.Depth);
            string? inheritedNamespace = GetInheritedNamespace(namespaceContextByDepth, reader.Depth);
            string? currentNamespace = reader.GetAttribute("ns") ?? inheritedNamespace;
            namespaceContextByDepth[reader.Depth] = currentNamespace;
            string? inheritedDatatypeLibrary = GetInheritedNamespace(datatypeLibraryContextByDepth, reader.Depth);
            string? currentDatatypeLibrary = reader.GetAttribute("datatypeLibrary") ?? inheritedDatatypeLibrary;
            datatypeLibraryContextByDepth[reader.Depth] = currentDatatypeLibrary;

            if (reader.LocalName == "element")
            {
                AddName(reader, state.Elements, currentNamespace);
            }
            else if (reader.LocalName == "attribute")
            {
                AddName(reader, state.Attributes, currentNamespace);
            }
            else if (reader.LocalName == "include" || reader.LocalName == "externalRef")
            {
                ReadExternalReference(reader, baseDirectory, state);
            }
            else if (reader.LocalName == "define")
            {
                ReadDefine(reader, state, currentNamespace, currentDatatypeLibrary);
            }
            else if (reader.LocalName == "start")
            {
                ReadStart(reader, state, currentNamespace, currentDatatypeLibrary);
            }
        }
    }

    private static void RemoveContextsBelow(Dictionary<int, string?> contextByDepth, int depth)
    {
        foreach (int existingDepth in contextByDepth.Keys.Where(existingDepth => existingDepth >= depth).ToArray())
        {
            contextByDepth.Remove(existingDepth);
        }
    }

    private static string? GetInheritedNamespace(Dictionary<int, string?> namespaceContextByDepth, int depth)
    {
        for (int currentDepth = depth - 1; currentDepth >= 0; currentDepth--)
        {
            if (namespaceContextByDepth.TryGetValue(currentDepth, out string? namespaceUri))
            {
                return namespaceUri;
            }
        }

        return null;
    }

    private static void AddName(XmlReader reader, Dictionary<string, SchemaNameMetadata> names, string? inheritedNamespace)
    {
        string? name = reader.GetAttribute("name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        SchemaNameMetadata? parsed = ParseName(reader, name, inheritedNamespace);
        if (parsed == null)
        {
            return;
        }

        names[parsed.NamespaceUri + "\u001f" + parsed.LocalName] = parsed;
    }

    private static SchemaNameMetadata? ParseName(XmlReader reader, string name, string? inheritedNamespace)
    {
        string? namespaceUri = null;
        string localName = name;

        int colonIndex = name.IndexOf(':');
        if (colonIndex >= 0)
        {
            string prefix = name.Substring(0, colonIndex);
            localName = name.Substring(colonIndex + 1);
            namespaceUri ??= reader.LookupNamespace(prefix);
            if (namespaceUri == null || namespaceUri == RelaxNgNamespace)
            {
                namespaceUri = ResolveKnownNamespace(prefix) ?? namespaceUri;
            }
        }
        else
        {
            namespaceUri = reader.GetAttribute("ns") ?? inheritedNamespace;
        }

        if (string.IsNullOrWhiteSpace(namespaceUri) || string.IsNullOrWhiteSpace(localName))
        {
            return null;
        }

        return new SchemaNameMetadata
        {
            NamespaceUri = namespaceUri,
            LocalName = localName
        };
    }

    private static string? ResolveKnownNamespace(string prefix)
    {
        return prefix switch
        {
            "anim" => "urn:oasis:names:tc:opendocument:xmlns:animation:1.0",
            "chart" => "urn:oasis:names:tc:opendocument:xmlns:chart:1.0",
            "config" => "urn:oasis:names:tc:opendocument:xmlns:config:1.0",
            "db" => "urn:oasis:names:tc:opendocument:xmlns:database:1.0",
            "dr3d" => "urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0",
            "draw" => "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0",
            "fo" => "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0",
            "form" => "urn:oasis:names:tc:opendocument:xmlns:form:1.0",
            "math" => "http://www.w3.org/1998/Math/MathML",
            "meta" => "urn:oasis:names:tc:opendocument:xmlns:meta:1.0",
            "number" => "urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0",
            "office" => "urn:oasis:names:tc:opendocument:xmlns:office:1.0",
            "presentation" => "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0",
            "script" => "urn:oasis:names:tc:opendocument:xmlns:script:1.0",
            "style" => "urn:oasis:names:tc:opendocument:xmlns:style:1.0",
            "svg" => "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0",
            "table" => "urn:oasis:names:tc:opendocument:xmlns:table:1.0",
            "text" => "urn:oasis:names:tc:opendocument:xmlns:text:1.0",
            "xlink" => "http://www.w3.org/1999/xlink",
            _ => null
        };
    }

    private static void ReadExternalReference(XmlReader reader, string baseDirectory, ReaderState state)
    {
        string? href = reader.GetAttribute("href");
        if (string.IsNullOrWhiteSpace(href))
        {
            return;
        }

        if (Uri.TryCreate(href, UriKind.Absolute, out Uri? absoluteUri) && !absoluteUri.IsFile)
        {
            state.ExternalReferences.Add(href);
            return;
        }

        string referencedPath = Uri.TryCreate(href, UriKind.Absolute, out absoluteUri) && absoluteUri.IsFile
            ? absoluteUri.LocalPath
            : Path.Combine(baseDirectory, href);
        string fullPath = Path.GetFullPath(referencedPath);
        if (!IsPathUnderDirectory(fullPath, state.RootDirectory))
        {
            state.RejectedReferences.Add(fullPath);
            return;
        }

        if (!File.Exists(fullPath))
        {
            state.MissingReferences.Add(fullPath);
            return;
        }

        ReadFileInto(fullPath, state);
    }

    private static bool IsPathUnderDirectory(string path, string rootDirectory)
    {
        string root = Path.GetFullPath(rootDirectory);
        if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) &&
            !root.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            root += Path.DirectorySeparatorChar;
        }

        string fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static void ReadDefine(
        XmlReader reader,
        ReaderState state,
        string? inheritedNamespace,
        string? inheritedDatatypeLibrary)
    {
        string? name = reader.GetAttribute("name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        SchemaPatternMetadata pattern = state.GetOrAddPattern(name);
        if (reader.IsEmptyElement)
        {
            return;
        }

        string? combine = reader.GetAttribute("combine");
        using XmlReader subtree = reader.ReadSubtree();
        var document = XDocument.Load(subtree, LoadOptions.None);
        ApplyInheritedNamespace(document.Root, inheritedNamespace);
        ApplyInheritedDatatypeLibrary(document.Root, inheritedDatatypeLibrary);
        var roots = new List<SchemaPatternNodeMetadata>();
        foreach (XElement child in document.Root?.Elements() ?? Enumerable.Empty<XElement>())
        {
            roots.Add(CreatePatternNode(child, "exactlyOne", insideExcept: false));
        }

        AddPatternRoots(pattern, roots, combine);

        ReadDefineElement(document.Root, pattern, state, "exactlyOne", insideExcept: false);
    }

    private static void ReadStart(
        XmlReader reader,
        ReaderState state,
        string? inheritedNamespace,
        string? inheritedDatatypeLibrary)
    {
        SchemaPatternMetadata pattern = state.GetOrAddPattern("start");
        if (reader.IsEmptyElement)
        {
            return;
        }

        using XmlReader subtree = reader.ReadSubtree();
        var document = XDocument.Load(subtree, LoadOptions.None);
        ApplyInheritedNamespace(document.Root, inheritedNamespace);
        ApplyInheritedDatatypeLibrary(document.Root, inheritedDatatypeLibrary);
        foreach (XElement child in document.Root?.Elements() ?? Enumerable.Empty<XElement>())
        {
            pattern.PatternTree.Add(CreatePatternNode(child, "exactlyOne", insideExcept: false));
        }

        ReadDefineElement(document.Root, pattern, state, "exactlyOne", insideExcept: false);
    }

    private static void ApplyInheritedNamespace(XElement? root, string? inheritedNamespace)
    {
        if (root == null ||
            inheritedNamespace == null ||
            root.Attribute("ns") != null)
        {
            return;
        }

        root.SetAttributeValue("ns", inheritedNamespace);
    }

    private static void ApplyInheritedDatatypeLibrary(XElement? root, string? inheritedDatatypeLibrary)
    {
        if (root == null ||
            inheritedDatatypeLibrary == null ||
            root.Attribute("datatypeLibrary") != null)
        {
            return;
        }

        root.SetAttributeValue("datatypeLibrary", inheritedDatatypeLibrary);
    }

    private static void AddPatternRoots(
        SchemaPatternMetadata pattern,
        List<SchemaPatternNodeMetadata> roots,
        string? combine)
    {
        if (roots.Count == 0)
        {
            return;
        }

        if (pattern.PatternTree.Count == 0 ||
            string.IsNullOrWhiteSpace(combine) ||
            string.Equals(combine, "choice", StringComparison.Ordinal))
        {
            pattern.PatternTree.AddRange(roots);
            return;
        }

        string kind = string.Equals(combine, "interleave", StringComparison.Ordinal)
            ? "interleave"
            : string.Equals(combine, "group", StringComparison.Ordinal)
                ? "group"
                : string.Empty;
        if (kind.Length == 0)
        {
            pattern.PatternTree.AddRange(roots);
            return;
        }

        var combined = new SchemaPatternNodeMetadata { Kind = kind };
        combined.Children.AddRange(pattern.PatternTree);
        combined.Children.AddRange(roots);
        pattern.PatternTree.Clear();
        pattern.PatternTree.Add(combined);
    }

    private static SchemaPatternNodeMetadata CreatePatternNode(
        XElement element,
        string occurrence,
        bool insideExcept)
    {
        string kind = element.Name.NamespaceName == RelaxNgNamespace
            ? element.Name.LocalName
            : element.Name.LocalName;
        string nextOccurrence = element.Name.NamespaceName == RelaxNgNamespace
            ? element.Name.LocalName switch
            {
                "optional" => "optional",
                "zeroOrMore" => "zeroOrMore",
                "oneOrMore" => "oneOrMore",
                _ => occurrence
            }
            : occurrence;
        bool nextInsideExcept = insideExcept ||
            (element.Name.NamespaceName == RelaxNgNamespace && element.Name.LocalName == "except");
        var node = new SchemaPatternNodeMetadata
        {
            Kind = kind,
            Occurrence = nextOccurrence
        };

        if (element.Name.NamespaceName == RelaxNgNamespace)
        {
            if (element.Name.LocalName == "element" || element.Name.LocalName == "attribute")
            {
                string? name = (string?)element.Attribute("name");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    SchemaNameMetadata? parsed = ParseName(element, name);
                    if (parsed != null)
                    {
                        node.NamespaceUri = parsed.NamespaceUri;
                        node.LocalName = parsed.LocalName;
                    }
                }
            }
            else if (element.Name.LocalName == "ref" ||
                element.Name.LocalName == "parentRef")
            {
                node.ReferenceName = ((string?)element.Attribute("name")) ?? string.Empty;
            }
            else if (element.Name.LocalName == "data")
            {
                node.DataType = ((string?)element.Attribute("type")) ?? string.Empty;
                node.DataTypeLibrary = GetInheritedRelaxNgDatatypeLibrary(element) ?? string.Empty;
                foreach (XElement parameter in element.Elements().Where(child =>
                    child.Name.NamespaceName == RelaxNgNamespace &&
                    child.Name.LocalName == "param"))
                {
                    string? parameterName = (string?)parameter.Attribute("name");
                    if (!string.IsNullOrWhiteSpace(parameterName))
                    {
                        node.DataParameters.Add(new SchemaDatatypeParameterMetadata
                        {
                            Name = parameterName,
                            Value = parameter.Value.Trim()
                        });
                    }
                }
            }
            else if (element.Name.LocalName == "value")
            {
                node.DataType = ((string?)element.Attribute("type")) ?? string.Empty;
                node.DataTypeLibrary = GetInheritedRelaxNgDatatypeLibrary(element) ?? string.Empty;
                node.Value = element.Value.Trim();
            }

            SchemaNameClassMetadata? nameClass = CreateNameClass(element, insideExcept);
            if (nameClass != null)
            {
                node.NameClasses.Add(nameClass);
            }
        }

        foreach (XElement child in element.Elements())
        {
            node.Children.Add(CreatePatternNode(child, nextOccurrence, nextInsideExcept));
        }

        return node;
    }

    private static void ReadDefineElement(
        XElement? element,
        SchemaPatternMetadata pattern,
        ReaderState state,
        string occurrence,
        bool insideExcept)
    {
        if (element == null)
        {
            return;
        }

        string nextOccurrence = element.Name.NamespaceName == RelaxNgNamespace
            ? element.Name.LocalName switch
            {
                "optional" => "optional",
                "zeroOrMore" => "zeroOrMore",
                "oneOrMore" => "oneOrMore",
                _ => occurrence
            }
            : occurrence;
        bool nextInsideExcept = insideExcept ||
            (element.Name.NamespaceName == RelaxNgNamespace && element.Name.LocalName == "except");

        if (element.Name.NamespaceName == RelaxNgNamespace)
        {
            AddPatternKind(element, pattern);
            AddNameClass(element, pattern, insideExcept);

            if (element.Name.LocalName == "element")
            {
                AddName(element, state.Elements);
                AddPatternNameUse(element, pattern.ChildElements, nextOccurrence);
            }
            else if (element.Name.LocalName == "attribute")
            {
                AddName(element, state.Attributes);
                AddPatternNameUse(element, pattern.Attributes, nextOccurrence);
            }
            else if (element.Name.LocalName == "ref" ||
                element.Name.LocalName == "parentRef")
            {
                string? referenceName = (string?)element.Attribute("name");
                if (!string.IsNullOrWhiteSpace(referenceName) && !pattern.References.Contains(referenceName, StringComparer.Ordinal))
                {
                    pattern.References.Add(referenceName);
                }
            }
        }

        foreach (XElement child in element.Elements())
        {
            ReadDefineElement(child, pattern, state, nextOccurrence, nextInsideExcept);
        }
    }

    private static void AddPatternKind(XElement element, SchemaPatternMetadata pattern)
    {
        string? kind = element.Name.LocalName switch
        {
            "choice" => "choice",
            "group" => "group",
            "interleave" => "interleave",
            "except" => "except",
            "parentRef" => "parentRef",
            "optional" => "optional",
            "zeroOrMore" => "zeroOrMore",
            "oneOrMore" => "oneOrMore",
            "list" => "list",
            "mixed" => "mixed",
            "data" => "data",
            "value" => "value",
            "text" => "text",
            "empty" => "empty",
            "notAllowed" => "notAllowed",
            "name" => "name",
            "nsName" => "nsName",
            "anyName" => "anyName",
            _ => null
        };

        if (kind != null && !pattern.PatternKinds.Contains(kind, StringComparer.Ordinal))
        {
            pattern.PatternKinds.Add(kind);
        }
    }

    private static void AddNameClass(XElement element, SchemaPatternMetadata pattern, bool insideExcept)
    {
        if (element.Name.NamespaceName != RelaxNgNamespace)
        {
            return;
        }

        SchemaNameClassMetadata? nameClass = CreateNameClass(element, insideExcept);

        if (nameClass == null)
        {
            return;
        }

        if (pattern.NameClasses.Any(item =>
            string.Equals(item.Kind, nameClass.Kind, StringComparison.Ordinal) &&
            string.Equals(item.NamespaceUri, nameClass.NamespaceUri, StringComparison.Ordinal) &&
            string.Equals(item.LocalName, nameClass.LocalName, StringComparison.Ordinal) &&
            item.IsExcept == nameClass.IsExcept))
        {
            return;
        }

        pattern.NameClasses.Add(nameClass);
    }

    private static SchemaNameClassMetadata? CreateNameClass(XElement element, bool insideExcept)
    {
        if (element.Name.NamespaceName != RelaxNgNamespace)
        {
            return null;
        }

        return element.Name.LocalName switch
        {
            "anyName" => new SchemaNameClassMetadata
            {
                Kind = "anyName",
                IsExcept = insideExcept
            },
            "nsName" => new SchemaNameClassMetadata
            {
                Kind = "nsName",
                NamespaceUri = GetInheritedRelaxNgNamespace(element) ?? string.Empty,
                IsExcept = insideExcept
            },
            "name" => CreateNameClassFromNameElement(element, insideExcept),
            _ => null
        };
    }

    private static SchemaNameClassMetadata? CreateNameClassFromNameElement(XElement element, bool insideExcept)
    {
        string text = element.Value.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        SchemaNameMetadata? parsed = ParseName(element, text);
        if (parsed == null)
        {
            return null;
        }

        return new SchemaNameClassMetadata
        {
            Kind = "name",
            NamespaceUri = parsed.NamespaceUri,
            LocalName = parsed.LocalName,
            IsExcept = insideExcept
        };
    }

    private static void AddName(XElement element, Dictionary<string, SchemaNameMetadata> names)
    {
        string? name = (string?)element.Attribute("name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        SchemaNameMetadata? parsed = ParseName(element, name);
        if (parsed == null)
        {
            return;
        }

        names[parsed.NamespaceUri + "\u001f" + parsed.LocalName] = parsed;
    }

    private static void AddPatternNameUse(XElement element, List<SchemaPatternNameUseMetadata> names, string occurrence)
    {
        string? name = (string?)element.Attribute("name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        SchemaNameMetadata? parsed = ParseName(element, name);
        if (parsed == null)
        {
            return;
        }

        if (names.Any(item =>
            string.Equals(item.NamespaceUri, parsed.NamespaceUri, StringComparison.Ordinal) &&
            string.Equals(item.LocalName, parsed.LocalName, StringComparison.Ordinal) &&
            string.Equals(item.Occurrence, occurrence, StringComparison.Ordinal)))
        {
            return;
        }

        names.Add(new SchemaPatternNameUseMetadata
        {
            NamespaceUri = parsed.NamespaceUri,
            LocalName = parsed.LocalName,
            Occurrence = occurrence
        });
    }

    private static SchemaNameMetadata? ParseName(XElement element, string name)
    {
        string? namespaceUri = null;
        string localName = name;

        int colonIndex = name.IndexOf(':');
        if (colonIndex >= 0)
        {
            string prefix = name.Substring(0, colonIndex);
            localName = name.Substring(colonIndex + 1);
            XNamespace? resolved = element.GetNamespaceOfPrefix(prefix);
            namespaceUri ??= resolved?.NamespaceName;
            if (namespaceUri == null || namespaceUri == RelaxNgNamespace)
            {
                namespaceUri = ResolveKnownNamespace(prefix) ?? namespaceUri;
            }
        }
        else
        {
            namespaceUri = GetInheritedRelaxNgNamespace(element);
        }

        if (string.IsNullOrWhiteSpace(namespaceUri) || string.IsNullOrWhiteSpace(localName))
        {
            return null;
        }

        return new SchemaNameMetadata
        {
            NamespaceUri = namespaceUri,
            LocalName = localName
        };
    }

    private static string? GetInheritedRelaxNgNamespace(XElement element)
    {
        foreach (XElement current in element.AncestorsAndSelf())
        {
            string? namespaceUri = (string?)current.Attribute("ns");
            if (namespaceUri != null)
            {
                return namespaceUri;
            }
        }

        return null;
    }

    private static string? GetInheritedRelaxNgDatatypeLibrary(XElement element)
    {
        foreach (XElement current in element.AncestorsAndSelf())
        {
            string? datatypeLibrary = (string?)current.Attribute("datatypeLibrary");
            if (datatypeLibrary != null)
            {
                return datatypeLibrary;
            }
        }

        return null;
    }

    private sealed class ReaderState
    {
        public ReaderState(string source, string rootDirectory)
        {
            Source = source;
            RootDirectory = Path.GetFullPath(rootDirectory);
        }

        public string Source { get; }

        public string RootDirectory { get; }

        public Dictionary<string, SchemaNameMetadata> Elements { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, SchemaNameMetadata> Attributes { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, SchemaPatternMetadata> Patterns { get; } = new(StringComparer.Ordinal);

        public HashSet<string> VisitedFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> MissingReferences { get; } = new();

        public List<string> ExternalReferences { get; } = new();

        public List<string> RejectedReferences { get; } = new();

        public SchemaPatternMetadata GetOrAddPattern(string name)
        {
            if (!Patterns.TryGetValue(name, out SchemaPatternMetadata? pattern))
            {
                pattern = new SchemaPatternMetadata { Name = name };
                Patterns[name] = pattern;
            }

            return pattern;
        }

        public SchemaMetadata ToMetadata()
        {
            var metadata = new SchemaMetadata { Source = Source };
            metadata.Elements.AddRange(Elements.Values.OrderBy(item => item.NamespaceUri, StringComparer.Ordinal).ThenBy(item => item.LocalName, StringComparer.Ordinal));
            metadata.Attributes.AddRange(Attributes.Values.OrderBy(item => item.NamespaceUri, StringComparer.Ordinal).ThenBy(item => item.LocalName, StringComparer.Ordinal));
            metadata.Patterns.AddRange(Patterns.Values
                .OrderBy(item => item.Name, StringComparer.Ordinal)
                .Select(item =>
                {
                    var copy = new SchemaPatternMetadata { Name = item.Name };
                    copy.References.AddRange(item.References.OrderBy(reference => reference, StringComparer.Ordinal));
                    copy.PatternKinds.AddRange(item.PatternKinds.OrderBy(kind => kind, StringComparer.Ordinal));
                    copy.ChildElements.AddRange(SortNameUses(item.ChildElements));
                    copy.Attributes.AddRange(SortNameUses(item.Attributes));
                    copy.NameClasses.AddRange(SortNameClasses(item.NameClasses));
                    copy.PatternTree.AddRange(item.PatternTree.Select(ClonePatternNode));
                    return copy;
                }));
            metadata.MissingReferences.AddRange(MissingReferences.OrderBy(item => item, StringComparer.Ordinal));
            metadata.ExternalReferences.AddRange(ExternalReferences.OrderBy(item => item, StringComparer.Ordinal));
            metadata.RejectedReferences.AddRange(RejectedReferences.OrderBy(item => item, StringComparer.Ordinal));
            return metadata;
        }

        private static IEnumerable<SchemaPatternNameUseMetadata> SortNameUses(IEnumerable<SchemaPatternNameUseMetadata> items)
        {
            return items
                .OrderBy(item => item.NamespaceUri, StringComparer.Ordinal)
                .ThenBy(item => item.LocalName, StringComparer.Ordinal)
                .ThenBy(item => item.Occurrence, StringComparer.Ordinal)
                .Select(item => new SchemaPatternNameUseMetadata
                {
                    NamespaceUri = item.NamespaceUri,
                    LocalName = item.LocalName,
                    Occurrence = item.Occurrence
                });
        }

        private static IEnumerable<SchemaNameClassMetadata> SortNameClasses(IEnumerable<SchemaNameClassMetadata> items)
        {
            return items
                .OrderBy(item => item.Kind, StringComparer.Ordinal)
                .ThenBy(item => item.NamespaceUri, StringComparer.Ordinal)
                .ThenBy(item => item.LocalName, StringComparer.Ordinal)
                .ThenBy(item => item.IsExcept)
                .Select(item => new SchemaNameClassMetadata
                {
                    Kind = item.Kind,
                    NamespaceUri = item.NamespaceUri,
                    LocalName = item.LocalName,
                    IsExcept = item.IsExcept
                });
        }

        private static SchemaPatternNodeMetadata ClonePatternNode(SchemaPatternNodeMetadata node)
        {
            var copy = new SchemaPatternNodeMetadata
            {
                Kind = node.Kind,
                Occurrence = node.Occurrence,
                NamespaceUri = node.NamespaceUri,
                LocalName = node.LocalName,
                ReferenceName = node.ReferenceName,
                DataType = node.DataType,
                DataTypeLibrary = node.DataTypeLibrary,
                Value = node.Value
            };
            copy.DataParameters.AddRange(node.DataParameters
                .OrderBy(item => item.Name, StringComparer.Ordinal)
                .ThenBy(item => item.Value, StringComparer.Ordinal)
                .Select(item => new SchemaDatatypeParameterMetadata
                {
                    Name = item.Name,
                    Value = item.Value
                }));
            copy.NameClasses.AddRange(SortNameClasses(node.NameClasses));
            copy.Children.AddRange(node.Children.Select(ClonePatternNode));
            return copy;
        }
    }
}
