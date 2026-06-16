using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace OdfKit.Core;

internal static class OdfRdfParser
{
    private const string RdfNamespace = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace RdfNs = RdfNamespace;

    public static OdfRdfMetadata Parse(Stream stream, long maxCharsInDocument = 0)
    {
        var metadata = new OdfRdfMetadata();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = maxCharsInDocument > 0 ? maxCharsInDocument : 0
        };

        XDocument document;
        using (var reader = XmlReader.Create(stream, settings))
        {
            document = XDocument.Load(reader);
        }

        foreach (var description in document.Descendants(RdfNs + "Description"))
        {
            string? subject = GetRdfAttribute(description, "about") ??
                GetRdfAttribute(description, "nodeID");
            if (string.IsNullOrWhiteSpace(subject))
            {
                continue;
            }

            foreach (var property in description.Elements())
            {
                if (property.Name.NamespaceName == RdfNamespace)
                {
                    continue;
                }

                string predicate = property.Name.NamespaceName + property.Name.LocalName;
                string? resource = GetRdfAttribute(property, "resource");
                if (!string.IsNullOrWhiteSpace(resource))
                {
                    metadata.AddLoadedTriple(new OdfRdfTriple(subject!, predicate, resource!, isLiteral: false));
                    continue;
                }

                string literal = property.Value;
                if (!string.IsNullOrWhiteSpace(literal))
                {
                    metadata.AddLoadedTriple(new OdfRdfTriple(subject!, predicate, literal, isLiteral: true));
                }
            }
        }

        metadata.AcceptChanges();
        return metadata;
    }

    public static byte[] Serialize(OdfRdfMetadata metadata, bool indent)
    {
        using var stream = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = indent
        };

        using (var writer = XmlWriter.Create(stream, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("rdf", "RDF", RdfNamespace);
            writer.WriteAttributeString("xmlns", "rdf", null, RdfNamespace);

            Dictionary<string, string> namespacePrefixes = new(StringComparer.Ordinal);
            int namespaceIndex = 1;

            foreach (var subjectGroup in metadata.Triples.GroupBy(triple => triple.Subject))
            {
                writer.WriteStartElement("rdf", "Description", RdfNamespace);
                writer.WriteAttributeString("rdf", "about", RdfNamespace, subjectGroup.Key);

                foreach (var triple in subjectGroup)
                {
                    var (namespaceUri, localName) = SplitPredicate(triple.Predicate);
                    if (!namespacePrefixes.TryGetValue(namespaceUri, out string? prefix))
                    {
                        prefix = "ns" + namespaceIndex++;
                        namespacePrefixes[namespaceUri] = prefix;
                    }

                    writer.WriteStartElement(prefix, localName, namespaceUri);
                    if (triple.IsLiteral)
                    {
                        writer.WriteString(triple.ObjectValue);
                    }
                    else
                    {
                        writer.WriteAttributeString("rdf", "resource", RdfNamespace, triple.ObjectValue);
                    }

                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        metadata.AcceptChanges();
        return stream.ToArray();
    }

    private static string? GetRdfAttribute(XElement element, string localName)
    {
        return element.Attribute(RdfNs + localName)?.Value;
    }

    private static (string NamespaceUri, string LocalName) SplitPredicate(string predicate)
    {
        int splitIndex = predicate.LastIndexOf('#');
        if (splitIndex < 0)
        {
            splitIndex = predicate.LastIndexOf('/');
        }

        if (splitIndex < 0 || splitIndex == predicate.Length - 1)
        {
            return (string.Empty, XmlConvert.EncodeLocalName(predicate));
        }

        string namespaceUri = predicate.Substring(0, splitIndex + 1);
        string localName = predicate.Substring(splitIndex + 1);
        try
        {
            XmlConvert.VerifyNCName(localName);
        }
        catch (XmlException)
        {
            localName = XmlConvert.EncodeLocalName(localName);
        }

        return (namespaceUri, localName);
    }
}
