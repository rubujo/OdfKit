using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.DOM;

namespace OdfKit.Core;

/// <summary>
/// Partial: Flat XML package serialization for archive writing.
/// Partial：封存寫入的 Flat XML 序列化。
/// </summary>
internal static partial class OdfPackageArchiveWriter
{
    private static async Task WriteFlatXmlToStreamAsync(
        OdfPackage.OdfPackageSaveCollaborators ctx,
        Stream targetStream,
        CancellationToken cancellationToken = default)
    {
        using Stream buffer = OdfPackageSaver.CreateTempStream(ctx, ctx.EstimateArchiveSize(), async: true);
        WriteFlatXmlToStream(ctx, buffer);
        cancellationToken.ThrowIfCancellationRequested();
        buffer.Position = 0;
        await buffer.CopyToAsync(targetStream, 81920, cancellationToken).ConfigureAwait(false);
    }

    private static void WriteFlatXmlToStream(OdfPackage.OdfPackageSaveCollaborators ctx, Stream targetStream)
    {
        XNamespace officeNs = XNamespace.Get(OdfNamespaces.Office);
        var xmlSettings = new XmlReaderSettings
        {
            NameTable = OdfXmlNameTable.Create(),
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = ctx.LoadOptions.MaxXmlCharactersInDocument > 0
                ? ctx.LoadOptions.MaxXmlCharactersInDocument
                : 0
        };

        XElement contentRoot;
        if (ctx.Entries.TryGetValue("content.xml", out OdfPackageEntry? contentEntry))
        {
            using var reader = XmlReader.Create(contentEntry.OpenReader(), xmlSettings);
            contentRoot = XDocument.Load(reader).Root ?? throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfPackageArchiveWriter_InvalidContentXmlRoot"));
        }
        else
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfPackageArchiveWriter_VirtualNotFound"));
        }

        XElement stylesRoot;
        if (ctx.Entries.TryGetValue("styles.xml", out OdfPackageEntry? stylesEntry))
        {
            using var reader = XmlReader.Create(stylesEntry.OpenReader(), xmlSettings);
            stylesRoot = XDocument.Load(reader).Root ?? throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfPackageArchiveWriter_InvalidStylesXmlRoot"));
        }
        else
        {
            stylesRoot = new XElement(officeNs + "document-styles");
        }

        XElement metaRoot;
        if (ctx.Entries.TryGetValue("meta.xml", out OdfPackageEntry? metaEntry))
        {
            using var reader = XmlReader.Create(metaEntry.OpenReader(), xmlSettings);
            metaRoot = XDocument.Load(reader).Root ?? throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfPackageArchiveWriter_InvalidMetaXmlRoot"));
        }
        else
        {
            metaRoot = new XElement(officeNs + "document-meta");
        }

        XElement settingsRoot;
        if (ctx.Entries.TryGetValue("settings.xml", out OdfPackageEntry? settingsEntry))
        {
            using var reader = XmlReader.Create(settingsEntry.OpenReader(), xmlSettings);
            settingsRoot = XDocument.Load(reader).Root ?? throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfPackageArchiveWriter_InvalidSettingsXmlRoot"));
        }
        else
        {
            settingsRoot = new XElement(officeNs + "document-settings");
        }

        var root = new XElement(officeNs + "document");

        string version = contentRoot.Attribute(officeNs + "version")?.Value ?? "1.3";
        root.SetAttributeValue(officeNs + "version", version);
        if (!string.IsNullOrEmpty(ctx.MimeType))
            root.SetAttributeValue(officeNs + "mimetype", ctx.MimeType);

        OdfPackageXmlNamespaceHelper.CopyNamespaces(contentRoot, root);
        OdfPackageXmlNamespaceHelper.CopyNamespaces(stylesRoot, root);
        OdfPackageXmlNamespaceHelper.CopyNamespaces(metaRoot, root);
        OdfPackageXmlNamespaceHelper.CopyNamespaces(settingsRoot, root);

        XElement? metaElement = metaRoot.Element(officeNs + "meta");
        if (metaElement is not null)
            root.Add(new XElement(metaElement));

        XElement? settingsElement = settingsRoot.Element(officeNs + "settings");
        if (settingsElement is not null)
            root.Add(new XElement(settingsElement));

        XElement? contentFontDecls = contentRoot.Element(officeNs + "font-face-decls");
        XElement? stylesFontDecls = stylesRoot.Element(officeNs + "font-face-decls");
        XElement? fontDecls = stylesFontDecls is not null
            ? new XElement(stylesFontDecls)
            : contentFontDecls is not null
                ? new XElement(contentFontDecls)
                : null;
        if (fontDecls is not null)
            root.Add(fontDecls);

        XElement? stylesElement = stylesRoot.Element(officeNs + "styles");
        if (stylesElement is not null)
            root.Add(new XElement(stylesElement));

        var combinedAutoStyles = new XElement(officeNs + "automatic-styles");
        var addedAutoStyleNames = new HashSet<string>(StringComparer.Ordinal);
        XElement? contentAuto = contentRoot.Element(officeNs + "automatic-styles");
        if (contentAuto is not null)
        {
            foreach (XElement element in contentAuto.Elements())
            {
                XAttribute? nameAttr = element.Attribute(XName.Get("name", OdfNamespaces.Style));
                if (nameAttr is not null)
                {
                    addedAutoStyleNames.Add(nameAttr.Value);
                }

                combinedAutoStyles.Add(new XElement(element));
            }
        }

        XElement? stylesAuto = stylesRoot.Element(officeNs + "automatic-styles");
        if (stylesAuto is not null)
        {
            foreach (XElement element in stylesAuto.Elements())
            {
                XAttribute? nameAttr = element.Attribute(XName.Get("name", OdfNamespaces.Style));
                if (nameAttr is not null && !addedAutoStyleNames.Add(nameAttr.Value))
                {
                    continue;
                }

                combinedAutoStyles.Add(new XElement(element));
            }
        }

        if (combinedAutoStyles.HasElements)
            root.Add(combinedAutoStyles);

        XElement? masterStyles = stylesRoot.Element(officeNs + "master-styles");
        if (masterStyles is not null)
            root.Add(new XElement(masterStyles));

        XElement? bodyElement = contentRoot.Element(officeNs + "body");
        if (bodyElement is not null)
            root.Add(new XElement(bodyElement));

        XNamespace xlinkNs = XNamespace.Get(OdfNamespaces.XLink);
        List<XElement> elementsWithHref = root.Descendants().Where(e => e.Attribute(xlinkNs + "href") is not null).ToList();

        foreach (XElement elem in elementsWithHref)
        {
            XAttribute hrefAttr = elem.Attribute(xlinkNs + "href")!;
            string href = hrefAttr.Value;
            if (href.StartsWith(OdfMediaManager.PicturesEntryPrefix, StringComparison.Ordinal))
            {
                if (ctx.Entries.TryGetValue(href, out OdfPackageEntry? entry))
                {
                    var binDataElement = new XElement(officeNs + "binary-data");
                    binDataElement.SetAttributeValue("href", href);
                    elem.Add(binDataElement);

                    hrefAttr.Remove();
                    elem.Attribute(xlinkNs + "type")?.Remove();
                    elem.Attribute(xlinkNs + "show")?.Remove();
                    elem.Attribute(xlinkNs + "actuate")?.Remove();
                }
            }
            else
            {
                string normHref = href.TrimStart('.', '/').TrimEnd('/');
                string subDocContentPath = $"{normHref}/content.xml";
                if (ctx.Entries.TryGetValue(subDocContentPath, out OdfPackageEntry? subDocEntry))
                {
                    string mimeType = "application/vnd.oasis.opendocument.formula";
                    string subDocMimePath = $"{normHref}/mimetype";
                    if (ctx.Entries.TryGetValue(subDocMimePath, out OdfPackageEntry? mimeEntry))
                    {
                        using var mimeReader = new StreamReader(mimeEntry.OpenReader(), Encoding.UTF8);
                        mimeType = mimeReader.ReadToEnd().Trim();
                    }
                    else if (ctx.Manifest.TryGetValue(normHref, out string? m))
                    {
                        mimeType = m;
                    }
                    else if (ctx.Manifest.TryGetValue(normHref + "/", out string? mSlash))
                    {
                        mimeType = mSlash;
                    }

                    XElement subDocRoot;
                    using (var subReader = XmlReader.Create(subDocEntry.OpenReader(), xmlSettings))
                        subDocRoot = XDocument.Load(subReader).Root
                            ?? throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfPackageArchiveWriter_InvalidRoot", subDocContentPath));

                    var nestedDoc = new XElement(officeNs + "document");
                    nestedDoc.SetAttributeValue(officeNs + "mimetype", mimeType);

                    string subDocVersion = subDocRoot.Attribute(officeNs + "version")?.Value ?? "1.3";
                    nestedDoc.SetAttributeValue(officeNs + "version", subDocVersion);

                    OdfPackageXmlNamespaceHelper.CopyNamespaces(subDocRoot, nestedDoc);

                    foreach (XElement child in subDocRoot.Elements())
                        nestedDoc.Add(new XElement(child));

                    elem.Add(nestedDoc);

                    hrefAttr.Remove();
                    elem.Attribute(xlinkNs + "type")?.Remove();
                    elem.Attribute(xlinkNs + "show")?.Remove();
                    elem.Attribute(xlinkNs + "actuate")?.Remove();
                }
            }
        }

        var writerSettings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = ctx.SaveOptions.IndentXml
        };
        using (var writer = XmlWriter.Create(targetStream, writerSettings))
        {
            WriteNodeStreaming(root, writer, ctx);
        }
    }

    private static void WriteNodeStreaming(XNode node, XmlWriter writer, OdfPackage.OdfPackageSaveCollaborators ctx)
    {
        if (node is XElement element)
        {
            XNamespace officeNs = XNamespace.Get(OdfNamespaces.Office);
            if (element.Name == officeNs + "binary-data" && element.Attribute("href") is XAttribute hrefAttr)
            {
                string href = hrefAttr.Value;
                writer.WriteStartElement("office", "binary-data", OdfNamespaces.Office);

                if (ctx.Entries.TryGetValue(href, out OdfPackageEntry? entry))
                {
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(65536);
                    try
                    {
                        using Stream stream = entry.OpenReader();
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            writer.WriteBase64(buffer, 0, bytesRead);
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }

                writer.WriteEndElement();
            }
            else
            {
                if (element.Name.Namespace == XNamespace.None)
                {
                    writer.WriteStartElement(element.Name.LocalName);
                }
                else
                {
                    string elementPrefix = element.GetPrefixOfNamespace(element.Name.Namespace) ?? string.Empty;
                    writer.WriteStartElement(elementPrefix, element.Name.LocalName, element.Name.NamespaceName);
                }

                foreach (XAttribute attr in element.Attributes())
                {
                    if (attr.Name.Namespace == XNamespace.None)
                    {
                        writer.WriteAttributeString(attr.Name.LocalName, attr.Value);
                    }
                    else
                    {
                        string attrPrefix = element.GetPrefixOfNamespace(attr.Name.Namespace) ?? string.Empty;
                        writer.WriteAttributeString(attrPrefix, attr.Name.LocalName, attr.Name.NamespaceName, attr.Value);
                    }
                }

                foreach (XNode child in element.Nodes())
                {
                    WriteNodeStreaming(child, writer, ctx);
                }

                writer.WriteEndElement();
            }
        }
        else if (node is XText text)
        {
            writer.WriteString(text.Value);
        }
        else if (node is XComment comment)
        {
            writer.WriteComment(comment.Value);
        }
        else if (node is XCData cdata)
        {
            writer.WriteCData(cdata.Value);
        }
        else if (node is XProcessingInstruction pi)
        {
            writer.WriteProcessingInstruction(pi.Target, pi.Data);
        }
    }
}
