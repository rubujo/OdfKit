using System;
using System.IO;
using System.Text;

namespace OdfKit.Tools.OdfSchemaGenerator;

/// <summary>
/// Writes schema metadata as deterministic JSON.
/// </summary>
public sealed class SchemaMetadataJsonWriter
{
    /// <summary>
    /// Writes schema metadata to a text writer.
    /// </summary>
    public void Write(SchemaMetadata metadata, TextWriter writer)
    {
        if (metadata == null) throw new ArgumentNullException(nameof(metadata));
        if (writer == null) throw new ArgumentNullException(nameof(writer));

        writer.WriteLine("{");
        writer.Write("  \"source\": ");
        WriteJsonString(writer, metadata.Source);
        writer.WriteLine(",");
        writer.Write("  \"sourceDate\": ");
        WriteJsonString(writer, metadata.SourceDate);
        writer.WriteLine(",");
        writer.WriteLine("  \"elements\": [");
        WriteNames(writer, metadata.Elements);
        writer.WriteLine("  ],");
        writer.WriteLine("  \"attributes\": [");
        WriteNames(writer, metadata.Attributes);
        writer.WriteLine("  ],");
        writer.WriteLine("  \"patterns\": [");
        WritePatterns(writer, metadata.Patterns);
        writer.WriteLine("  ],");
        writer.WriteLine("  \"missingReferences\": [");
        WriteStrings(writer, metadata.MissingReferences);
        writer.WriteLine("  ],");
        writer.WriteLine("  \"externalReferences\": [");
        WriteStrings(writer, metadata.ExternalReferences);
        writer.WriteLine("  ],");
        writer.WriteLine("  \"rejectedReferences\": [");
        WriteStrings(writer, metadata.RejectedReferences);
        writer.WriteLine("  ]");
        writer.WriteLine("}");
    }

    private static void WriteNames(TextWriter writer, System.Collections.Generic.IReadOnlyList<SchemaNameMetadata> names)
    {
        for (int i = 0; i < names.Count; i++)
        {
            SchemaNameMetadata name = names[i];
            writer.Write("    { \"namespaceUri\": ");
            WriteJsonString(writer, name.NamespaceUri);
            writer.Write(", \"localName\": ");
            WriteJsonString(writer, name.LocalName);
            writer.Write(" }");
            if (i + 1 < names.Count)
            {
                writer.Write(",");
            }

            writer.WriteLine();
        }
    }

    private static void WritePatterns(TextWriter writer, System.Collections.Generic.IReadOnlyList<SchemaPatternMetadata> patterns)
    {
        for (int i = 0; i < patterns.Count; i++)
        {
            SchemaPatternMetadata pattern = patterns[i];
            writer.Write("    { \"name\": ");
            WriteJsonString(writer, pattern.Name);
            writer.Write(", \"references\": [");
            WriteStringsInline(writer, pattern.References);
            writer.Write("], \"patternKinds\": [");
            WriteStringsInline(writer, pattern.PatternKinds);
            writer.Write("], \"childElements\": [");
            WriteNameUsesInline(writer, pattern.ChildElements);
            writer.Write("], \"attributes\": [");
            WriteNameUsesInline(writer, pattern.Attributes);
            writer.Write("], \"nameClasses\": [");
            WriteNameClassesInline(writer, pattern.NameClasses);
            writer.Write("], \"patternTree\": [");
            WritePatternNodesInline(writer, pattern.PatternTree);
            writer.Write("] }");
            if (i + 1 < patterns.Count)
            {
                writer.Write(",");
            }

            writer.WriteLine();
        }
    }

    private static void WriteStringsInline(TextWriter writer, System.Collections.Generic.IReadOnlyList<string> values)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(", ");
            }

            WriteJsonString(writer, values[i]);
        }
    }

    private static void WriteNameUsesInline(TextWriter writer, System.Collections.Generic.IReadOnlyList<SchemaPatternNameUseMetadata> names)
    {
        for (int i = 0; i < names.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(", ");
            }

            SchemaPatternNameUseMetadata name = names[i];
            writer.Write("{ \"namespaceUri\": ");
            WriteJsonString(writer, name.NamespaceUri);
            writer.Write(", \"localName\": ");
            WriteJsonString(writer, name.LocalName);
            writer.Write(", \"occurrence\": ");
            WriteJsonString(writer, name.Occurrence);
            writer.Write(" }");
        }
    }

    private static void WriteNameClassesInline(TextWriter writer, System.Collections.Generic.IReadOnlyList<SchemaNameClassMetadata> names)
    {
        for (int i = 0; i < names.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(", ");
            }

            SchemaNameClassMetadata name = names[i];
            writer.Write("{ \"kind\": ");
            WriteJsonString(writer, name.Kind);
            writer.Write(", \"namespaceUri\": ");
            WriteJsonString(writer, name.NamespaceUri);
            writer.Write(", \"localName\": ");
            WriteJsonString(writer, name.LocalName);
            writer.Write(", \"isExcept\": ");
            writer.Write(name.IsExcept ? "true" : "false");
            writer.Write(" }");
        }
    }

    private static void WritePatternNodesInline(TextWriter writer, System.Collections.Generic.IReadOnlyList<SchemaPatternNodeMetadata> nodes)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(", ");
            }

            WritePatternNodeInline(writer, nodes[i]);
        }
    }

    private static void WritePatternNodeInline(TextWriter writer, SchemaPatternNodeMetadata node)
    {
        writer.Write("{ \"kind\": ");
        WriteJsonString(writer, node.Kind);
        writer.Write(", \"occurrence\": ");
        WriteJsonString(writer, node.Occurrence);
        writer.Write(", \"namespaceUri\": ");
        WriteJsonString(writer, node.NamespaceUri);
        writer.Write(", \"localName\": ");
        WriteJsonString(writer, node.LocalName);
        writer.Write(", \"referenceName\": ");
        WriteJsonString(writer, node.ReferenceName);
        writer.Write(", \"dataType\": ");
        WriteJsonString(writer, node.DataType);
        writer.Write(", \"dataTypeLibrary\": ");
        WriteJsonString(writer, node.DataTypeLibrary);
        writer.Write(", \"value\": ");
        WriteJsonString(writer, node.Value);
        writer.Write(", \"dataParameters\": [");
        WriteDataParametersInline(writer, node.DataParameters);
        writer.Write("]");
        writer.Write(", \"nameClasses\": [");
        WriteNameClassesInline(writer, node.NameClasses);
        writer.Write("], \"children\": [");
        WritePatternNodesInline(writer, node.Children);
        writer.Write("] }");
    }

    private static void WriteDataParametersInline(TextWriter writer, System.Collections.Generic.IReadOnlyList<SchemaDatatypeParameterMetadata> parameters)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(", ");
            }

            SchemaDatatypeParameterMetadata parameter = parameters[i];
            writer.Write("{ \"name\": ");
            WriteJsonString(writer, parameter.Name);
            writer.Write(", \"value\": ");
            WriteJsonString(writer, parameter.Value);
            writer.Write(" }");
        }
    }

    private static void WriteStrings(TextWriter writer, System.Collections.Generic.IReadOnlyList<string> values)
    {
        for (int i = 0; i < values.Count; i++)
        {
            writer.Write("    ");
            WriteJsonString(writer, values[i]);
            if (i + 1 < values.Count)
            {
                writer.Write(",");
            }

            writer.WriteLine();
        }
    }

    private static void WriteJsonString(TextWriter writer, string value)
    {
        writer.Write('"');
        foreach (char ch in value)
        {
            switch (ch)
            {
                case '\\':
                    writer.Write("\\\\");
                    break;
                case '"':
                    writer.Write("\\\"");
                    break;
                case '\r':
                    writer.Write("\\r");
                    break;
                case '\n':
                    writer.Write("\\n");
                    break;
                case '\t':
                    writer.Write("\\t");
                    break;
                default:
                    if (char.IsControl(ch))
                    {
                        writer.Write("\\u");
                        writer.Write(((int)ch).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        writer.Write(ch);
                    }

                    break;
            }
        }

        writer.Write('"');
    }
}
