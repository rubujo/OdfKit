using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using OdfKit.DOM;

namespace OdfKit.Core;

/// <summary>
/// ODF 封裝 ZIP 與 Flat XML 寫入引擎（內部協作者）。
/// </summary>
internal static class OdfPackageArchiveWriter
{
    /// <summary>
    /// 將封裝項目寫入目標串流（ZIP 或 Flat XML）。
    /// </summary>
    internal static void WriteToArchive(OdfPackage.OdfPackageSaveCollaborators ctx, Stream targetStream)
    {
        if (ctx.IsFlatXml)
        {
            WriteFlatXmlToStream(ctx, targetStream);
            return;
        }

        using var zip = new ZipArchive(targetStream, ZipArchiveMode.Create, true, Encoding.UTF8);

        if (ctx.Entries.TryGetValue("mimetype", out OdfPackageEntry? mimeEntry))
        {
            ZipArchiveEntry zipEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);

            if (ctx.SaveOptions.Deterministic)
                zipEntry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            using (Stream entryStream = zipEntry.Open())
            using (Stream src = mimeEntry.OpenReader())
                src.CopyTo(entryStream);
        }

        foreach (KeyValuePair<string, OdfPackageEntry> kvp in ctx.Entries)
        {
            if (kvp.Key == "mimetype")
                continue;

            CompressionLevel compLevel = kvp.Value.IsCompressed
                ? ctx.SaveOptions.CompressionLevel
                : CompressionLevel.NoCompression;
            ZipArchiveEntry zipEntry = zip.CreateEntry(kvp.Key, compLevel);

            if (ctx.SaveOptions.Deterministic)
                zipEntry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            using (Stream entryStream = zipEntry.Open())
            using (Stream src = kvp.Value.OpenReader())
                src.CopyTo(entryStream);
        }
    }

    /// <summary>
    /// 將封裝項目非同步寫入目標串流（ZIP 或 Flat XML），支援協作式取消。
    /// </summary>
    internal static async Task WriteToArchiveAsync(
        OdfPackage.OdfPackageSaveCollaborators ctx,
        Stream targetStream,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ctx.IsFlatXml)
        {
            await WriteFlatXmlToStreamAsync(ctx, targetStream, cancellationToken).ConfigureAwait(false);
            return;
        }

        using var zip = new ZipArchive(targetStream, ZipArchiveMode.Create, true, Encoding.UTF8);

        if (ctx.Entries.TryGetValue("mimetype", out OdfPackageEntry? mimeEntry))
        {
            ZipArchiveEntry zipEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);

            if (ctx.SaveOptions.Deterministic)
                zipEntry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            using (Stream entryStream = zipEntry.Open())
            using (Stream src = mimeEntry.OpenReader())
                await CopyEntryContentAsync(src, entryStream, cancellationToken).ConfigureAwait(false);
        }

        foreach (KeyValuePair<string, OdfPackageEntry> kvp in ctx.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (kvp.Key == "mimetype")
                continue;

            CompressionLevel compLevel = kvp.Value.IsCompressed
                ? ctx.SaveOptions.CompressionLevel
                : CompressionLevel.NoCompression;
            ZipArchiveEntry zipEntry = zip.CreateEntry(kvp.Key, compLevel);

            if (ctx.SaveOptions.Deterministic)
                zipEntry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            using (Stream entryStream = zipEntry.Open())
            using (Stream src = kvp.Value.OpenReader())
                await CopyEntryContentAsync(src, entryStream, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Task CopyEntryContentAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        return source.CopyToAsync(destination, 81920, cancellationToken);
    }

    private static async Task WriteFlatXmlToStreamAsync(
        OdfPackage.OdfPackageSaveCollaborators ctx,
        Stream targetStream,
        CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
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
            contentRoot = XDocument.Load(reader).Root ?? throw new InvalidDataException("Invalid content.xml root");
        }
        else
        {
            throw new InvalidDataException("Missing virtual content.xml");
        }

        XElement stylesRoot;
        if (ctx.Entries.TryGetValue("styles.xml", out OdfPackageEntry? stylesEntry))
        {
            using var reader = XmlReader.Create(stylesEntry.OpenReader(), xmlSettings);
            stylesRoot = XDocument.Load(reader).Root ?? throw new InvalidDataException("Invalid styles.xml root");
        }
        else
        {
            stylesRoot = new XElement(officeNs + "document-styles");
        }

        XElement metaRoot;
        if (ctx.Entries.TryGetValue("meta.xml", out OdfPackageEntry? metaEntry))
        {
            using var reader = XmlReader.Create(metaEntry.OpenReader(), xmlSettings);
            metaRoot = XDocument.Load(reader).Root ?? throw new InvalidDataException("Invalid meta.xml root");
        }
        else
        {
            metaRoot = new XElement(officeNs + "document-meta");
        }

        XElement settingsRoot;
        if (ctx.Entries.TryGetValue("settings.xml", out OdfPackageEntry? settingsEntry))
        {
            using var reader = XmlReader.Create(settingsEntry.OpenReader(), xmlSettings);
            settingsRoot = XDocument.Load(reader).Root ?? throw new InvalidDataException("Invalid settings.xml root");
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
        XElement? contentAuto = contentRoot.Element(officeNs + "automatic-styles");
        if (contentAuto is not null)
            combinedAutoStyles.Add(contentAuto.Elements());

        XElement? stylesAuto = stylesRoot.Element(officeNs + "automatic-styles");
        if (stylesAuto is not null)
        {
            foreach (XElement element in stylesAuto.Elements())
            {
                XAttribute? nameAttr = element.Attribute(XName.Get("name", OdfNamespaces.Style));
                if (nameAttr is not null)
                {
                    XElement? existing = combinedAutoStyles.Elements()
                        .FirstOrDefault(e => e.Attribute(XName.Get("name", OdfNamespaces.Style))?.Value == nameAttr.Value);
                    if (existing is not null)
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
            if (href.StartsWith("Pictures/", StringComparison.Ordinal))
            {
                if (ctx.Entries.TryGetValue(href, out OdfPackageEntry? entry))
                {
                    byte[] imageBytes;
                    using (Stream entryReader = entry.OpenReader())
                    using (var ms = new MemoryStream())
                    {
                        entryReader.CopyTo(ms);
                        imageBytes = ms.ToArray();
                    }

                    string base64 = Convert.ToBase64String(imageBytes);
                    var binDataElement = new XElement(officeNs + "binary-data", base64);
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
                            ?? throw new InvalidDataException($"Invalid {subDocContentPath} root");

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
            root.Save(writer);
    }
}
