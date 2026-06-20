using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.DOM;

namespace OdfKit.Core;

/// <summary>
/// Flat XML 格式 ODF 封裝載入引擎（內部協作者）。
/// </summary>
internal static class OdfPackageFlatXmlLoader
{
    /// <summary>
    /// 將 Flat XML 串流解析為虛擬 ZIP 項目結構。
    /// </summary>
    internal static void Initialize(OdfPackage.OdfPackageLoadCollaborators ctx, byte[] signature, int signatureLength)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            CloseInput = !ctx.LeaveOpen,
            MaxCharactersInDocument = ctx.LoadOptions.MaxXmlCharactersInDocument > 0
                ? ctx.LoadOptions.MaxXmlCharactersInDocument
                : 0
        };

        Stream xmlStream = ctx.UnderlyingStream!;
        if (!ctx.UnderlyingStream!.CanSeek && signatureLength > 0)
            xmlStream = new PeekableStream(ctx.UnderlyingStream, signature, signatureLength, ctx.LeaveOpen);

        var cleanXmlStream = new MemoryStream();
        var binaryPaths = new Dictionary<int, string>();
        int binaryCounter = 0;

        using (var reader = XmlReader.Create(xmlStream, settings))
        using (var writer = XmlWriter.Create(cleanXmlStream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false, Indent = false }))
        {
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        bool isEmpty = reader.IsEmptyElement;
                        if (reader.LocalName == "binary-data" && reader.NamespaceURI == OdfNamespaces.Office)
                        {
                            binaryCounter++;
                            byte[] buffer = ArrayPool<byte>.Shared.Rent(65536);
                            using (var binMs = new MemoryStream())
                            {
                                try
                                {
                                    int bytesRead;
                                    while ((bytesRead = reader.ReadElementContentAsBase64(buffer, 0, buffer.Length)) > 0)
                                    {
                                        binMs.Write(buffer, 0, bytesRead);
                                    }
                                }
                                finally
                                {
                                    ArrayPool<byte>.Shared.Return(buffer);
                                }

                                byte[] bytes = binMs.ToArray();
                                OdfMediaManager.DetectImageFormat(bytes, out string mediaType, out string ext);
                                string finalPath = $"Pictures/image_{binaryCounter}{ext}";
                                binaryPaths[binaryCounter] = finalPath;

                                ctx.Entries[finalPath] = new OdfPackageEntry(finalPath, bytes);
                                ctx.Manifest[finalPath] = mediaType;
                                ctx.EntryOrder.Add(finalPath);
                            }

                            writer.WriteStartElement("office", "binary-data", OdfNamespaces.Office);
                            writer.WriteAttributeString("office", "binary-index", OdfNamespaces.Office, binaryCounter.ToString());
                            writer.WriteEndElement();
                        }
                        else
                        {
                            writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
                            if (reader.HasAttributes)
                            {
                                while (reader.MoveToNextAttribute())
                                {
                                    writer.WriteAttributeString(reader.Prefix, reader.LocalName, reader.NamespaceURI, reader.Value);
                                }
                                reader.MoveToElement();
                            }
                            if (isEmpty)
                            {
                                writer.WriteEndElement();
                            }
                        }
                        break;

                    case XmlNodeType.EndElement:
                        writer.WriteEndElement();
                        break;

                    case XmlNodeType.Text:
                        writer.WriteString(reader.Value);
                        break;

                    case XmlNodeType.SignificantWhitespace:
                    case XmlNodeType.Whitespace:
                        writer.WriteWhitespace(reader.Value);
                        break;

                    case XmlNodeType.CDATA:
                        writer.WriteCData(reader.Value);
                        break;

                    case XmlNodeType.Comment:
                        writer.WriteComment(reader.Value);
                        break;

                    case XmlNodeType.XmlDeclaration:
                    case XmlNodeType.ProcessingInstruction:
                        writer.WriteProcessingInstruction(reader.Name, reader.Value);
                        break;
                }
            }
        }

        cleanXmlStream.Position = 0;
        XDocument doc;
        using (var cleanReader = XmlReader.Create(cleanXmlStream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
        {
            doc = XDocument.Load(cleanReader);
        }

        XElement? root = doc.Root;
        if (root is null || root.Name.LocalName != "document" || root.Name.NamespaceName != OdfNamespaces.Office)
            throw new InvalidDataException("Invalid Flat XML: root element must be office:document.");

        XNamespace officeNs = XNamespace.Get(OdfNamespaces.Office);
        XNamespace xlinkNs = XNamespace.Get(OdfNamespaces.XLink);

        XAttribute? mimeAttr = root.Attribute(officeNs + "mimetype") ?? root.Attribute("mimetype");
        ctx.MimeType = mimeAttr?.Value;
        if (string.IsNullOrEmpty(ctx.MimeType) && ctx.LoadOptions.ValidateMimeType)
            throw new InvalidDataException("Invalid Flat XML: missing office:mimetype.");

        XAttribute? versionAttr = root.Attribute(officeNs + "version") ?? root.Attribute("version");
        string version = versionAttr?.Value ?? "1.3";
        ctx.Version = version switch
        {
            "1.0" => OdfVersion.Odf10,
            "1.1" => OdfVersion.Odf11,
            "1.2" => OdfVersion.Odf12,
            "1.3" => OdfVersion.Odf13,
            "1.4" => OdfVersion.Odf14,
            _ => OdfVersion.Odf14
        };

        List<XElement> nestedDocs = doc.Descendants(officeNs + "document")
            .Where(d => d != doc.Root)
            .ToList();

        int objectCounter = 1;
        foreach (XElement nestedDoc in nestedDocs)
        {
            XElement? parent = nestedDoc.Parent;
            if (parent is null || parent.Name.LocalName != "object" || parent.Name.NamespaceName != OdfNamespaces.Draw)
                continue;

            string mimeType = nestedDoc.Attribute(officeNs + "mimetype")?.Value
                              ?? nestedDoc.Attribute("mimetype")?.Value
                              ?? "application/vnd.oasis.opendocument.formula";

            string? objectId = parent.Attribute(XNamespace.Get(OdfNamespaces.XLink) + "href")?.Value;
            if (string.IsNullOrEmpty(objectId))
                objectId = $"Object_{objectCounter++}";
            else
                objectId = objectId!.TrimStart('.', '/').TrimEnd('/');

            string subDocVersion = nestedDoc.Attribute(officeNs + "version")?.Value ?? "1.3";

            var subDocRoot = new XElement(officeNs + "document-content",
                new XAttribute(officeNs + "version", subDocVersion));

            OdfPackageXmlNamespaceHelper.CopyNamespaces(nestedDoc, subDocRoot);
            foreach (XElement child in nestedDoc.Elements())
                subDocRoot.Add(new XElement(child));

            byte[] contentBytes;
            using (var ms = new MemoryStream())
            {
                new XDocument(subDocRoot).Save(ms);
                contentBytes = ms.ToArray();
            }

            string folderPath = objectId;
            string contentPath = $"{folderPath}/content.xml";
            string mimePath = $"{folderPath}/mimetype";

            ctx.Entries[contentPath] = new OdfPackageEntry(contentPath, contentBytes);
            ctx.Manifest[contentPath] = "text/xml";
            ctx.EntryOrder.Add(contentPath);

            byte[] mimeBytes = Encoding.UTF8.GetBytes(mimeType);
            ctx.Entries[mimePath] = new OdfPackageEntry(mimePath, mimeBytes);
            ctx.Manifest[mimePath] = mimeType;
            ctx.EntryOrder.Add(mimePath);

            ctx.Manifest[folderPath + "/"] = mimeType;

            nestedDoc.Remove();

            XNamespace xlinkNsFormula = XNamespace.Get(OdfNamespaces.XLink);
            parent.SetAttributeValue(xlinkNsFormula + "href", folderPath);
            parent.SetAttributeValue(xlinkNsFormula + "type", "simple");
            parent.SetAttributeValue(xlinkNsFormula + "show", "embed");
            parent.SetAttributeValue(xlinkNsFormula + "actuate", "onLoad");
        }

        XElement? metaElement = root.Element(officeNs + "meta");
        XElement? settingsElement = root.Element(officeNs + "settings");
        XElement? stylesElement = root.Element(officeNs + "styles");
        XElement? autoStylesElement = root.Element(officeNs + "automatic-styles");
        XElement? masterStylesElement = root.Element(officeNs + "master-styles");
        XElement? fontDeclsElement = root.Element(officeNs + "font-face-decls");
        XElement? bodyElement = root.Element(officeNs + "body");

        List<XElement> binaryDataElements = doc.Descendants(officeNs + "binary-data").ToList();
        foreach (XElement binData in binaryDataElements)
        {
            XAttribute? indexAttr = binData.Attribute(officeNs + "binary-index") ?? binData.Attribute("binary-index");
            if (indexAttr != null && int.TryParse(indexAttr.Value, out int idx) && binaryPaths.TryGetValue(idx, out string? imagePath))
            {
                XElement? binParent = binData.Parent;
                if (binParent is not null)
                {
                    binData.Remove();

                    xlinkNs = XNamespace.Get(OdfNamespaces.XLink);

                    binParent.SetAttributeValue(xlinkNs + "href", imagePath);
                    binParent.SetAttributeValue(xlinkNs + "type", "simple");
                    binParent.SetAttributeValue(xlinkNs + "show", "embed");
                    binParent.SetAttributeValue(xlinkNs + "actuate", "onLoad");
                }
            }
        }

        var contentRoot = new XElement(officeNs + "document-content",
            new XAttribute(officeNs + "version", version));
        OdfPackageXmlNamespaceHelper.CopyNamespaces(root, contentRoot);

        if (fontDeclsElement is not null)
            contentRoot.Add(new XElement(fontDeclsElement));
        if (autoStylesElement is not null)
            contentRoot.Add(new XElement(autoStylesElement));
        if (bodyElement is not null)
            contentRoot.Add(new XElement(bodyElement));

        var stylesRoot = new XElement(officeNs + "document-styles",
            new XAttribute(officeNs + "version", version));
        OdfPackageXmlNamespaceHelper.CopyNamespaces(root, stylesRoot);

        if (fontDeclsElement is not null)
            stylesRoot.Add(new XElement(fontDeclsElement));
        if (stylesElement is not null)
            stylesRoot.Add(new XElement(stylesElement));
        if (autoStylesElement is not null)
            stylesRoot.Add(new XElement(autoStylesElement));
        if (masterStylesElement is not null)
            stylesRoot.Add(new XElement(masterStylesElement));

        var metaRoot = new XElement(officeNs + "document-meta",
            new XAttribute(officeNs + "version", version));
        OdfPackageXmlNamespaceHelper.CopyNamespaces(root, metaRoot);

        if (metaElement is not null)
            metaRoot.Add(new XElement(metaElement));
        else
            metaRoot.Add(new XElement(officeNs + "meta"));

        var settingsRoot = new XElement(officeNs + "document-settings",
            new XAttribute(officeNs + "version", version));
        OdfPackageXmlNamespaceHelper.CopyNamespaces(root, settingsRoot);

        if (settingsElement is not null)
            settingsRoot.Add(new XElement(settingsElement));
        else
            settingsRoot.Add(new XElement(officeNs + "settings"));

        byte[] ToUtf8Bytes(XElement element)
        {
            using var ms = new MemoryStream();
            var writerSettings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = ctx.SaveOptions.IndentXml
            };
            using (var writer = XmlWriter.Create(ms, writerSettings))
                element.Save(writer);
            return ms.ToArray();
        }

        ctx.WriteVirtualEntry("content.xml", ToUtf8Bytes(contentRoot), "text/xml");
        ctx.WriteVirtualEntry("styles.xml", ToUtf8Bytes(stylesRoot), "text/xml");
        ctx.WriteVirtualEntry("meta.xml", ToUtf8Bytes(metaRoot), "text/xml");
        ctx.WriteVirtualEntry("settings.xml", ToUtf8Bytes(settingsRoot), "text/xml");
        if (!string.IsNullOrEmpty(ctx.MimeType))
            ctx.WriteVirtualEntry("mimetype", Encoding.UTF8.GetBytes(ctx.MimeType), string.Empty);
    }

    /// <summary>
    /// 非同步將 Flat XML 串流解析為虛擬 ZIP 項目結構；不可搜尋串流會先緩衝至記憶體。
    /// </summary>
    internal static async Task InitializeAsync(
        OdfPackage.OdfPackageLoadCollaborators ctx,
        byte[] signature,
        int signatureLength,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ctx.UnderlyingStream is null)
            throw new InvalidOperationException("No input stream available.");

        if (!ctx.UnderlyingStream.CanSeek)
        {
            var ms = new MemoryStream();
            if (signatureLength > 0)
                ms.Write(signature, 0, signatureLength);

            await ctx.UnderlyingStream.CopyToAsync(ms, 81920, cancellationToken).ConfigureAwait(false);
            ms.Position = 0;
            if (!ctx.LeaveOpen)
                ctx.UnderlyingStream.Dispose();

            ctx.UnderlyingStream = ms;
            Initialize(ctx, Array.Empty<byte>(), 0);
            return;
        }

        Initialize(ctx, signature, signatureLength);
    }
}
