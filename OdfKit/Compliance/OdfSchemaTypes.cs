#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OdfKit.Compliance
{
    /// <summary>
    /// Identifies the schema role an ODF element plays during low-level validation.
    /// </summary>
    public enum OdfSchemaElementRole
    {
        /// <summary>
        /// A general ODF element.
        /// </summary>
        Element,

        /// <summary>
        /// A root element for one of the package XML streams or a flat XML document.
        /// </summary>
        DocumentRoot,

        /// <summary>
        /// A direct content kind element under office:body.
        /// </summary>
        BodyContent
    }

    /// <summary>
    /// Identifies a RELAX NG name class kind preserved in schema metadata.
    /// </summary>
    public enum OdfSchemaNameClassKind
    {
        /// <summary>
        /// A concrete name class.
        /// </summary>
        Name,

        /// <summary>
        /// A namespace-wide name class.
        /// </summary>
        NamespaceName,

        /// <summary>
        /// A wildcard name class.
        /// </summary>
        AnyName
    }

    /// <summary>
    /// Identifies a RELAX NG pattern node kind preserved in schema metadata.
    /// </summary>
    public enum OdfSchemaPatternNodeKind
    {
        /// <summary>
        /// A named element pattern.
        /// </summary>
        Element,

        /// <summary>
        /// A named attribute pattern.
        /// </summary>
        Attribute,

        /// <summary>
        /// A reference to another named pattern.
        /// </summary>
        Ref,

        /// <summary>
        /// A sequence/group pattern.
        /// </summary>
        Group,

        /// <summary>
        /// A choice pattern.
        /// </summary>
        Choice,

        /// <summary>
        /// An interleave pattern.
        /// </summary>
        Interleave,

        /// <summary>
        /// An optional occurrence wrapper.
        /// </summary>
        Optional,

        /// <summary>
        /// A zero-or-more occurrence wrapper.
        /// </summary>
        ZeroOrMore,

        /// <summary>
        /// A one-or-more occurrence wrapper.
        /// </summary>
        OneOrMore,

        /// <summary>
        /// A RELAX NG except pattern.
        /// </summary>
        Except,

        /// <summary>
        /// A literal text pattern.
        /// </summary>
        Text,

        /// <summary>
        /// An empty content pattern.
        /// </summary>
        Empty,

        /// <summary>
        /// A notAllowed pattern.
        /// </summary>
        NotAllowed,

        /// <summary>
        /// A datatype pattern.
        /// </summary>
        Data,

        /// <summary>
        /// A literal value pattern.
        /// </summary>
        Value,

        /// <summary>
        /// A list pattern.
        /// </summary>
        List,

        /// <summary>
        /// A mixed content pattern.
        /// </summary>
        Mixed,

        /// <summary>
        /// A concrete name class pattern.
        /// </summary>
        Name,

        /// <summary>
        /// A namespace-wide name class pattern.
        /// </summary>
        NamespaceName,

        /// <summary>
        /// A wildcard name class pattern.
        /// </summary>
        AnyName,

        /// <summary>
        /// A known but currently unclassified pattern kind.
        /// </summary>
        Other
    }

    /// <summary>
    /// Identifies a namespace-qualified ODF name without relying on XML prefixes.
    /// </summary>
    public sealed class OdfQualifiedName : IEquatable<OdfQualifiedName>
    {
        /// <summary>
        /// Initializes a new qualified name.
        /// </summary>
        public OdfQualifiedName(string namespaceUri, string localName)
        {
            if (string.IsNullOrWhiteSpace(namespaceUri)) throw new ArgumentException("Namespace URI cannot be empty.", nameof(namespaceUri));
            if (string.IsNullOrWhiteSpace(localName)) throw new ArgumentException("Local name cannot be empty.", nameof(localName));
            NamespaceUri = namespaceUri;
            LocalName = localName;
        }

        /// <summary>
        /// Gets the namespace URI.
        /// </summary>
        public string NamespaceUri { get; }

        /// <summary>
        /// Gets the local element or attribute name.
        /// </summary>
        public string LocalName { get; }

        /// <inheritdoc />
        public bool Equals(OdfQualifiedName? other)
        {
            return other != null &&
                string.Equals(NamespaceUri, other.NamespaceUri, StringComparison.Ordinal) &&
                string.Equals(LocalName, other.LocalName, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => Equals(obj as OdfQualifiedName);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(NamespaceUri) * 397) ^
                    StringComparer.Ordinal.GetHashCode(LocalName);
            }
        }

        /// <inheritdoc />
        public override string ToString() => "{" + NamespaceUri + "}" + LocalName;
    }

    /// <summary>
    /// Describes one element known to an ODF schema metadata set.
    /// </summary>
    public sealed class OdfElementDefinition
    {
        /// <summary>
        /// Initializes a new element definition.
        /// </summary>
        public OdfElementDefinition(
            OdfQualifiedName name,
            OdfSchemaElementRole role,
            OdfVersionRange supportedVersions,
            OdfDocumentKind documentKind = OdfDocumentKind.Unknown)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Role = role;
            SupportedVersions = supportedVersions ?? throw new ArgumentNullException(nameof(supportedVersions));
            DocumentKind = documentKind;
        }

        /// <summary>
        /// Gets the namespace-qualified element name.
        /// </summary>
        public OdfQualifiedName Name { get; }

        /// <summary>
        /// Gets the schema role of this element.
        /// </summary>
        public OdfSchemaElementRole Role { get; }

        /// <summary>
        /// Gets the ODF versions in which this element is supported.
        /// </summary>
        public OdfVersionRange SupportedVersions { get; }

        /// <summary>
        /// Gets the document kind represented by this element when applicable.
        /// </summary>
        public OdfDocumentKind DocumentKind { get; }
    }

    /// <summary>
    /// Describes one attribute known to an ODF schema metadata set.
    /// </summary>
    public sealed class OdfAttributeDefinition
    {
        /// <summary>
        /// Initializes a new attribute definition.
        /// </summary>
        public OdfAttributeDefinition(
            OdfQualifiedName name,
            string valueType,
            OdfVersionRange supportedVersions,
            bool isRequiredOnDocumentRoot = false)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
            SupportedVersions = supportedVersions ?? throw new ArgumentNullException(nameof(supportedVersions));
            IsRequiredOnDocumentRoot = isRequiredOnDocumentRoot;
        }

        /// <summary>
        /// Gets the namespace-qualified attribute name.
        /// </summary>
        public OdfQualifiedName Name { get; }

        /// <summary>
        /// Gets the schema value type name.
        /// </summary>
        public string ValueType { get; }

        /// <summary>
        /// Gets the ODF versions in which this attribute is supported.
        /// </summary>
        public OdfVersionRange SupportedVersions { get; }

        /// <summary>
        /// Gets a value indicating whether this attribute is required on ODF document roots.
        /// </summary>
        public bool IsRequiredOnDocumentRoot { get; }
    }

    /// <summary>
    /// Describes a RELAX NG name class constraint preserved for schema-driven validation.
    /// </summary>
    public sealed class OdfSchemaNameClass
    {
        /// <summary>
        /// Initializes a new name class metadata item.
        /// </summary>
        public OdfSchemaNameClass(
            OdfSchemaNameClassKind kind,
            string namespaceUri,
            string localName,
            bool isExcept)
        {
            Kind = kind;
            NamespaceUri = namespaceUri ?? string.Empty;
            LocalName = localName ?? string.Empty;
            IsExcept = isExcept;
        }

        /// <summary>
        /// Gets the RELAX NG name class kind.
        /// </summary>
        public OdfSchemaNameClassKind Kind { get; }

        /// <summary>
        /// Gets the namespace URI when the name class constrains one.
        /// </summary>
        public string NamespaceUri { get; }

        /// <summary>
        /// Gets the local name when the name class constrains one.
        /// </summary>
        public string LocalName { get; }

        /// <summary>
        /// Gets whether this name class appears under an rng:except node.
        /// </summary>
        public bool IsExcept { get; }

        /// <summary>
        /// Returns true when the supplied namespace-qualified name matches this name class.
        /// </summary>
        public bool Matches(string namespaceUri, string localName)
        {
            namespaceUri = namespaceUri ?? string.Empty;
            localName = localName ?? string.Empty;

            switch (Kind)
            {
                case OdfSchemaNameClassKind.Name:
                    return string.Equals(NamespaceUri, namespaceUri, StringComparison.Ordinal) &&
                        string.Equals(LocalName, localName, StringComparison.Ordinal);
                case OdfSchemaNameClassKind.NamespaceName:
                    return string.Equals(NamespaceUri, namespaceUri, StringComparison.Ordinal);
                case OdfSchemaNameClassKind.AnyName:
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Describes one named RELAX NG pattern preserved from schema metadata.
    /// </summary>
    public sealed class OdfSchemaPatternDefinition
    {
        private readonly IReadOnlyList<OdfSchemaPatternNode> _roots;

        /// <summary>
        /// Initializes a new named pattern definition.
        /// </summary>
        public OdfSchemaPatternDefinition(string name, IEnumerable<OdfSchemaPatternNode> roots)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Pattern name cannot be empty.", nameof(name));
            Name = name;
            _roots = new List<OdfSchemaPatternNode>(roots ?? throw new ArgumentNullException(nameof(roots))).AsReadOnly();
        }

        /// <summary>
        /// Gets the RELAX NG define name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets root nodes for this named pattern.
        /// </summary>
        public IReadOnlyList<OdfSchemaPatternNode> Roots => _roots;
    }

    /// <summary>
    /// Describes one datatype parameter attached to a RELAX NG data pattern.
    /// </summary>
    public sealed class OdfSchemaDatatypeParameter
    {
        /// <summary>
        /// Initializes a new datatype parameter.
        /// </summary>
        public OdfSchemaDatatypeParameter(string name, string value)
        {
            Name = name ?? string.Empty;
            Value = value ?? string.Empty;
        }

        /// <summary>
        /// Gets the parameter name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the parameter value.
        /// </summary>
        public string Value { get; }
    }

    /// <summary>
    /// Describes one RELAX NG pattern node preserved for future schema validation.
    /// </summary>
    public sealed class OdfSchemaPatternNode
    {
        private readonly IReadOnlyList<OdfSchemaNameClass> _nameClasses;
        private readonly IReadOnlyList<OdfSchemaPatternNode> _children;
        private readonly IReadOnlyList<OdfSchemaDatatypeParameter> _dataParameters;

        /// <summary>
        /// Initializes a new pattern node metadata item.
        /// </summary>
        public OdfSchemaPatternNode(
            OdfSchemaPatternNodeKind kind,
            string occurrence,
            string namespaceUri,
            string localName,
            string referenceName,
            string dataType,
            string value,
            IEnumerable<OdfSchemaNameClass>? nameClasses = null,
            IEnumerable<OdfSchemaPatternNode>? children = null,
            IEnumerable<KeyValuePair<string, string>>? dataParameters = null,
            string? dataTypeLibrary = null)
        {
            Kind = kind;
            Occurrence = string.IsNullOrWhiteSpace(occurrence) ? "exactlyOne" : occurrence;
            NamespaceUri = namespaceUri ?? string.Empty;
            LocalName = localName ?? string.Empty;
            ReferenceName = referenceName ?? string.Empty;
            DataType = dataType ?? string.Empty;
            DataTypeLibrary = dataTypeLibrary ?? string.Empty;
            Value = value ?? string.Empty;
            _nameClasses = new List<OdfSchemaNameClass>(nameClasses ?? Array.Empty<OdfSchemaNameClass>()).AsReadOnly();
            _children = new List<OdfSchemaPatternNode>(children ?? Array.Empty<OdfSchemaPatternNode>()).AsReadOnly();
            var parameters = new List<OdfSchemaDatatypeParameter>();
            foreach (KeyValuePair<string, string> parameter in dataParameters ?? Array.Empty<KeyValuePair<string, string>>())
            {
                if (!string.IsNullOrWhiteSpace(parameter.Key))
                {
                    parameters.Add(new OdfSchemaDatatypeParameter(parameter.Key, parameter.Value));
                }
            }

            _dataParameters = parameters.AsReadOnly();
        }

        /// <summary>
        /// Gets the RELAX NG pattern node kind.
        /// </summary>
        public OdfSchemaPatternNodeKind Kind { get; }

        /// <summary>
        /// Gets the nearest occurrence wrapper captured for this node.
        /// </summary>
        public string Occurrence { get; }

        /// <summary>
        /// Gets the namespace URI when this node carries a qualified name.
        /// </summary>
        public string NamespaceUri { get; }

        /// <summary>
        /// Gets the local name when this node carries a qualified name.
        /// </summary>
        public string LocalName { get; }

        /// <summary>
        /// Gets the referenced pattern name for reference nodes.
        /// </summary>
        public string ReferenceName { get; }

        /// <summary>
        /// Gets the datatype name for datatype nodes.
        /// </summary>
        public string DataType { get; }

        /// <summary>
        /// Gets the RELAX NG datatype library URI for datatype or value nodes.
        /// </summary>
        public string DataTypeLibrary { get; }

        /// <summary>
        /// Gets the literal value for value nodes.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Gets datatype parameters such as RELAX NG param facets for data nodes.
        /// </summary>
        public IReadOnlyList<OdfSchemaDatatypeParameter> DataParameters => _dataParameters;

        /// <summary>
        /// Gets name classes attached directly to this node.
        /// </summary>
        public IReadOnlyList<OdfSchemaNameClass> NameClasses => _nameClasses;

        /// <summary>
        /// Gets child pattern nodes.
        /// </summary>
        public IReadOnlyList<OdfSchemaPatternNode> Children => _children;
    }

    /// <summary>
    /// Represents deterministic schema metadata for one ODF version.
    /// </summary>
    public sealed class OdfSchemaSet
    {
        private readonly IReadOnlyDictionary<OdfQualifiedName, OdfElementDefinition> _elements;
        private readonly IReadOnlyDictionary<OdfQualifiedName, OdfAttributeDefinition> _attributes;
        private readonly IReadOnlyList<OdfSchemaNameClass> _nameClasses;
        private readonly IReadOnlyDictionary<string, OdfSchemaPatternDefinition> _patterns;

        /// <summary>
        /// Initializes a new schema metadata set.
        /// </summary>
        public OdfSchemaSet(
            OdfVersion version,
            Uri sourceUrl,
            string sourceDate,
            IEnumerable<OdfElementDefinition> elements,
            IEnumerable<OdfAttributeDefinition>? attributes = null,
            IEnumerable<OdfSchemaNameClass>? nameClasses = null,
            IEnumerable<OdfSchemaPatternDefinition>? patterns = null)
        {
            Version = version;
            SourceUrl = sourceUrl ?? throw new ArgumentNullException(nameof(sourceUrl));
            SourceDate = sourceDate ?? throw new ArgumentNullException(nameof(sourceDate));

            var byName = new Dictionary<OdfQualifiedName, OdfElementDefinition>();
            foreach (OdfElementDefinition element in elements ?? throw new ArgumentNullException(nameof(elements)))
            {
                byName[element.Name] = element;
            }

            _elements = new ReadOnlyDictionary<OdfQualifiedName, OdfElementDefinition>(byName);

            var attributesByName = new Dictionary<OdfQualifiedName, OdfAttributeDefinition>();
            foreach (OdfAttributeDefinition attribute in attributes ?? Array.Empty<OdfAttributeDefinition>())
            {
                attributesByName[attribute.Name] = attribute;
            }

            _attributes = new ReadOnlyDictionary<OdfQualifiedName, OdfAttributeDefinition>(attributesByName);
            _nameClasses = new List<OdfSchemaNameClass>(nameClasses ?? Array.Empty<OdfSchemaNameClass>()).AsReadOnly();

            var patternsByName = new Dictionary<string, OdfSchemaPatternDefinition>(StringComparer.Ordinal);
            foreach (OdfSchemaPatternDefinition pattern in patterns ?? Array.Empty<OdfSchemaPatternDefinition>())
            {
                patternsByName[pattern.Name] = pattern;
            }

            _patterns = new ReadOnlyDictionary<string, OdfSchemaPatternDefinition>(patternsByName);
        }

        /// <summary>
        /// Gets the ODF version represented by this metadata set.
        /// </summary>
        public OdfVersion Version { get; }

        /// <summary>
        /// Gets the official source URL used for this schema metadata set.
        /// </summary>
        public Uri SourceUrl { get; }

        /// <summary>
        /// Gets the source date as an ISO date string.
        /// </summary>
        public string SourceDate { get; }

        /// <summary>
        /// Gets all element definitions keyed by namespace URI and local name.
        /// </summary>
        public IReadOnlyDictionary<OdfQualifiedName, OdfElementDefinition> Elements => _elements;

        /// <summary>
        /// Gets all attribute definitions keyed by namespace URI and local name.
        /// </summary>
        public IReadOnlyDictionary<OdfQualifiedName, OdfAttributeDefinition> Attributes => _attributes;

        /// <summary>
        /// Gets RELAX NG name classes preserved from the source schema.
        /// </summary>
        public IReadOnlyList<OdfSchemaNameClass> NameClasses => _nameClasses;

        /// <summary>
        /// Gets named RELAX NG pattern trees keyed by define name.
        /// </summary>
        public IReadOnlyDictionary<string, OdfSchemaPatternDefinition> Patterns => _patterns;

        /// <summary>
        /// Creates a new schema set by merging this metadata with another schema set.
        /// </summary>
        public OdfSchemaSet MergeWith(OdfSchemaSet additional, bool overwriteExisting = false)
        {
            if (additional == null) throw new ArgumentNullException(nameof(additional));

            var elements = new Dictionary<OdfQualifiedName, OdfElementDefinition>();
            foreach (var pair in _elements)
            {
                elements[pair.Key] = pair.Value;
            }

            foreach (var pair in additional.Elements)
            {
                if (overwriteExisting || !elements.ContainsKey(pair.Key))
                {
                    elements[pair.Key] = pair.Value;
                }
            }

            var attributes = new Dictionary<OdfQualifiedName, OdfAttributeDefinition>();
            foreach (var pair in _attributes)
            {
                attributes[pair.Key] = pair.Value;
            }

            foreach (var pair in additional.Attributes)
            {
                if (overwriteExisting || !attributes.ContainsKey(pair.Key))
                {
                    attributes[pair.Key] = pair.Value;
                }
            }

            var nameClasses = new List<OdfSchemaNameClass>(_nameClasses);
            nameClasses.AddRange(additional.NameClasses);

            var patterns = new Dictionary<string, OdfSchemaPatternDefinition>(StringComparer.Ordinal);
            foreach (var pair in _patterns)
            {
                patterns[pair.Key] = pair.Value;
            }

            foreach (var pair in additional.Patterns)
            {
                if (overwriteExisting || !patterns.ContainsKey(pair.Key))
                {
                    patterns[pair.Key] = pair.Value;
                }
            }

            return new OdfSchemaSet(
                additional.Version,
                additional.SourceUrl,
                additional.SourceDate,
                elements.Values,
                attributes.Values,
                nameClasses,
                patterns.Values);
        }

        /// <summary>
        /// Finds an element definition by namespace URI and local name.
        /// </summary>
        public OdfElementDefinition? FindElement(string namespaceUri, string localName)
        {
            return _elements.TryGetValue(new OdfQualifiedName(namespaceUri, localName), out OdfElementDefinition? element)
                ? element
                : null;
        }

        /// <summary>
        /// Returns true when an element exists in this metadata set.
        /// </summary>
        public bool ContainsElement(string namespaceUri, string localName) => FindElement(namespaceUri, localName) != null;

        /// <summary>
        /// Finds an attribute definition by namespace URI and local name.
        /// </summary>
        public OdfAttributeDefinition? FindAttribute(string namespaceUri, string localName)
        {
            return _attributes.TryGetValue(new OdfQualifiedName(namespaceUri, localName), out OdfAttributeDefinition? attribute)
                ? attribute
                : null;
        }

        /// <summary>
        /// Finds a named RELAX NG pattern tree by define name.
        /// </summary>
        public OdfSchemaPatternDefinition? FindPattern(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return _patterns.TryGetValue(name, out OdfSchemaPatternDefinition? pattern)
                ? pattern
                : null;
        }

        /// <summary>
        /// Finds all preserved RELAX NG name classes that match a namespace-qualified name.
        /// </summary>
        public IReadOnlyList<OdfSchemaNameClass> FindMatchingNameClasses(string namespaceUri, string localName)
        {
            var matches = new List<OdfSchemaNameClass>();
            foreach (OdfSchemaNameClass nameClass in _nameClasses)
            {
                if (nameClass.Matches(namespaceUri, localName))
                {
                    matches.Add(nameClass);
                }
            }

            return matches.AsReadOnly();
        }

        /// <summary>
        /// Returns true when the flat preserved name class set allows a namespace-qualified name.
        /// </summary>
        /// <remarks>
        /// This helper evaluates the preserved name class metadata without RELAX NG pattern context:
        /// at least one non-except name class must match, and no matching except name class may apply.
        /// Full schema validation must still evaluate the surrounding RELAX NG pattern tree.
        /// </remarks>
        public bool IsNameAllowedByNameClasses(string namespaceUri, string localName)
        {
            bool allowed = false;
            foreach (OdfSchemaNameClass nameClass in _nameClasses)
            {
                if (!nameClass.Matches(namespaceUri, localName))
                {
                    continue;
                }

                if (nameClass.IsExcept)
                {
                    return false;
                }

                allowed = true;
            }

            return allowed;
        }
    }
}
