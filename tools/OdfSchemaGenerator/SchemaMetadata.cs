using System.Collections.Generic;

namespace OdfKit.Tools.OdfSchemaGenerator;

/// <summary>
/// Represents schema metadata extracted from a RELAX NG file.
/// </summary>
public sealed class SchemaMetadata
{
    /// <summary>
    /// Gets or sets the schema source path or URI.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the schema source date.
    /// </summary>
    public string SourceDate { get; set; } = "generated";

    /// <summary>
    /// Gets or sets the ODF version string (e.g. "1.1", "1.2", "1.3", "1.4") this schema represents.
    /// </summary>
    public string Version { get; set; } = "1.4";

    /// <summary>
    /// Gets the extracted elements.
    /// </summary>
    public List<SchemaNameMetadata> Elements { get; } = new();

    /// <summary>
    /// Gets the extracted attributes.
    /// </summary>
    public List<SchemaNameMetadata> Attributes { get; } = new();

    /// <summary>
    /// Gets the extracted RELAX NG named patterns.
    /// </summary>
    public List<SchemaPatternMetadata> Patterns { get; } = new();

    /// <summary>
    /// Gets relative references that could not be resolved on disk.
    /// </summary>
    public List<string> MissingReferences { get; } = new();

    /// <summary>
    /// Gets absolute non-file references recorded but not fetched by the generator.
    /// </summary>
    public List<string> ExternalReferences { get; } = new();

    /// <summary>
    /// Gets file references rejected because they resolve outside the schema root directory.
    /// </summary>
    public List<string> RejectedReferences { get; } = new();
}

/// <summary>
/// Represents one namespace-qualified schema name.
/// </summary>
public sealed class SchemaNameMetadata
{
    /// <summary>
    /// Gets or sets the namespace URI.
    /// </summary>
    public string NamespaceUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the local name.
    /// </summary>
    public string LocalName { get; set; } = string.Empty;
}

/// <summary>
/// Represents one named RELAX NG pattern and the patterns it references.
/// </summary>
public sealed class SchemaPatternMetadata
{
    /// <summary>
    /// Gets or sets the pattern name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets referenced pattern names.
    /// </summary>
    public List<string> References { get; } = new();

    /// <summary>
    /// Gets RELAX NG pattern kinds used by this named pattern.
    /// </summary>
    public List<string> PatternKinds { get; } = new();

    /// <summary>
    /// Gets element names that can appear inside this pattern.
    /// </summary>
    public List<SchemaPatternNameUseMetadata> ChildElements { get; } = new();

    /// <summary>
    /// Gets attribute names that can appear inside this pattern.
    /// </summary>
    public List<SchemaPatternNameUseMetadata> Attributes { get; } = new();

    /// <summary>
    /// Gets wildcard/name-class constraints used by this pattern.
    /// </summary>
    public List<SchemaNameClassMetadata> NameClasses { get; } = new();

    /// <summary>
    /// Gets root pattern nodes preserved from this named pattern.
    /// </summary>
    public List<SchemaPatternNodeMetadata> PatternTree { get; } = new();
}

/// <summary>
/// Represents a namespace-qualified name used by a RELAX NG pattern.
/// </summary>
public sealed class SchemaPatternNameUseMetadata
{
    /// <summary>
    /// Gets or sets the namespace URI.
    /// </summary>
    public string NamespaceUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the local name.
    /// </summary>
    public string LocalName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the nearest RELAX NG occurrence wrapper.
    /// </summary>
    public string Occurrence { get; set; } = "exactlyOne";
}

/// <summary>
/// Represents a RELAX NG name class such as name, nsName, or anyName.
/// </summary>
public sealed class SchemaNameClassMetadata
{
    /// <summary>
    /// Gets or sets the RELAX NG name class kind.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the namespace URI when available.
    /// </summary>
    public string NamespaceUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the local name when available.
    /// </summary>
    public string LocalName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this name class appears under rng:except.
    /// </summary>
    public bool IsExcept { get; set; }
}

/// <summary>
/// Represents one RELAX NG pattern node with enough context for a future validator.
/// </summary>
public sealed class SchemaPatternNodeMetadata
{
    /// <summary>
    /// Gets or sets the RELAX NG pattern kind.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the nearest RELAX NG occurrence wrapper.
    /// </summary>
    public string Occurrence { get; set; } = "exactlyOne";

    /// <summary>
    /// Gets or sets the namespace URI when this node carries a qualified name.
    /// </summary>
    public string NamespaceUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the local name when this node carries a qualified name.
    /// </summary>
    public string LocalName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the referenced pattern name for rng:ref nodes.
    /// </summary>
    public string ReferenceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the datatype name for rng:data nodes.
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the RELAX NG datatype library URI for rng:data or rng:value nodes.
    /// </summary>
    public string DataTypeLibrary { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the literal value for rng:value nodes.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets datatype parameters attached to rng:data nodes.
    /// </summary>
    public List<SchemaDatatypeParameterMetadata> DataParameters { get; } = new();

    /// <summary>
    /// Gets name classes attached directly to this node.
    /// </summary>
    public List<SchemaNameClassMetadata> NameClasses { get; } = new();

    /// <summary>
    /// Gets child pattern nodes.
    /// </summary>
    public List<SchemaPatternNodeMetadata> Children { get; } = new();
}

/// <summary>
/// Represents one RELAX NG datatype parameter.
/// </summary>
public sealed class SchemaDatatypeParameterMetadata
{
    /// <summary>
    /// Gets or sets the parameter name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameter value.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
