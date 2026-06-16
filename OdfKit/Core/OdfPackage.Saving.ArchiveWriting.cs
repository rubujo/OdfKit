using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using OdfKit.DOM;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    #region Archive and Flat XML Writing

    private void WriteToArchive(Stream targetStream)
    {
        if (_isFlatXml)
        {
            WriteFlatXmlToStream(targetStream);
            return;
        }

        using var zip = new ZipArchive(targetStream, ZipArchiveMode.Create, true, Encoding.UTF8);

        // 1. mimetype 必須為第一個項目且以 Stored（未壓縮）方式寫入
        if (_entries.TryGetValue("mimetype", out var mimeEntry))
        {
            var zipEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);

            // 啟用確定性輸出時，使用固定 ZIP 時間戳記。
            if (_saveOptions.Deterministic)
            {
                zipEntry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            }

            using (var entryStream = zipEntry.Open())
            using (var src = mimeEntry.OpenReader())
            {
                src.CopyTo(entryStream);
            }
        }

        // 2. 寫入其餘所有項目
        foreach (var kvp in _entries)
        {
            if (kvp.Key == "mimetype")
                continue;

            var compLevel = kvp.Value.IsCompressed ? _saveOptions.CompressionLevel : CompressionLevel.NoCompression;
            var zipEntry = zip.CreateEntry(kvp.Key, compLevel);

            if (_saveOptions.Deterministic)
            {
                zipEntry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            }

            using (var entryStream = zipEntry.Open())
            using (var src = kvp.Value.OpenReader())
            {
                src.CopyTo(entryStream);
            }
        }
    }

    private void WriteFlatXmlToStream(Stream targetStream)
    {
        var officeNs = XNamespace.Get(OdfNamespaces.Office);
        var xmlSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = _loadOptions.MaxXmlCharactersInDocument > 0 ? _loadOptions.MaxXmlCharactersInDocument : 0
        };

        // 讀取 content.xml
        XElement contentRoot;
        if (_entries.TryGetValue("content.xml", out var contentEntry))
        {
            using var reader = XmlReader.Create(contentEntry.OpenReader(), xmlSettings);
            contentRoot = XDocument.Load(reader).Root ?? throw new InvalidDataException("Invalid content.xml root");
        }
        else
        {
            throw new InvalidDataException("Missing virtual content.xml");
        }

        // 讀取 styles.xml
        XElement stylesRoot;
        if (_entries.TryGetValue("styles.xml", out var stylesEntry))
        {
            using var reader = XmlReader.Create(stylesEntry.OpenReader(), xmlSettings);
            stylesRoot = XDocument.Load(reader).Root ?? throw new InvalidDataException("Invalid styles.xml root");
        }
        else
        {
            stylesRoot = new XElement(officeNs + "document-styles");
        }

        // 讀取 meta.xml
        XElement metaRoot;
        if (_entries.TryGetValue("meta.xml", out var metaEntry))
        {
            using var reader = XmlReader.Create(metaEntry.OpenReader(), xmlSettings);
            metaRoot = XDocument.Load(reader).Root ?? throw new InvalidDataException("Invalid meta.xml root");
        }
        else
        {
            metaRoot = new XElement(officeNs + "document-meta");
        }

        // 讀取 settings.xml
        XElement settingsRoot;
        if (_entries.TryGetValue("settings.xml", out var settingsEntry))
        {
            using var reader = XmlReader.Create(settingsEntry.OpenReader(), xmlSettings);
            settingsRoot = XDocument.Load(reader).Root ?? throw new InvalidDataException("Invalid settings.xml root");
        }
        else
        {
            settingsRoot = new XElement(officeNs + "document-settings");
        }

        // 建構新的 office:document
        var root = new XElement(officeNs + "document");

        // 複製版本與 mimetype
        string version = contentRoot.Attribute(officeNs + "version")?.Value ?? "1.3";
        root.SetAttributeValue(officeNs + "version", version);
        if (!string.IsNullOrEmpty(_mimetype))
        {
            root.SetAttributeValue(officeNs + "mimetype", _mimetype);
        }

        // 複製命名空間宣告
        CopyNamespaces(contentRoot, root);
        CopyNamespaces(stylesRoot, root);
        CopyNamespaces(metaRoot, root);
        CopyNamespaces(settingsRoot, root);

        // 1. meta 區段
        var metaElement = metaRoot.Element(officeNs + "meta");
        if (metaElement != null)
        {
            root.Add(new XElement(metaElement));
        }

        // 2. settings 區段
        var settingsElement = settingsRoot.Element(officeNs + "settings");
        if (settingsElement != null)
        {
            root.Add(new XElement(settingsElement));
        }

        // 3. font-face-decls 區段
        var contentFontDecls = contentRoot.Element(officeNs + "font-face-decls");
        var stylesFontDecls = stylesRoot.Element(officeNs + "font-face-decls");
        XElement? fontDecls = null;
        if (stylesFontDecls != null)
        {
            fontDecls = new XElement(stylesFontDecls);
        }
        else if (contentFontDecls != null)
        {
            fontDecls = new XElement(contentFontDecls);
        }
        if (fontDecls != null)
        {
            root.Add(fontDecls);
        }

        // 4. styles 區段
        var stylesElement = stylesRoot.Element(officeNs + "styles");
        if (stylesElement != null)
        {
            root.Add(new XElement(stylesElement));
        }

        // 5. automatic-styles 區段
        var combinedAutoStyles = new XElement(officeNs + "automatic-styles");
        var contentAuto = contentRoot.Element(officeNs + "automatic-styles");
        if (contentAuto != null)
        {
            combinedAutoStyles.Add(contentAuto.Elements());
        }
        var stylesAuto = stylesRoot.Element(officeNs + "automatic-styles");
        if (stylesAuto != null)
        {
            foreach (var element in stylesAuto.Elements())
            {
                var nameAttr = element.Attribute(XName.Get("name", OdfNamespaces.Style));
                if (nameAttr != null)
                {
                    var existing = combinedAutoStyles.Elements().FirstOrDefault(e => e.Attribute(XName.Get("name", OdfNamespaces.Style))?.Value == nameAttr.Value);
                    if (existing != null)
                        continue;
                }
                combinedAutoStyles.Add(new XElement(element));
            }
        }
        if (combinedAutoStyles.HasElements)
        {
            root.Add(combinedAutoStyles);
        }

        // 6. master-styles 區段
        var masterStyles = stylesRoot.Element(officeNs + "master-styles");
        if (masterStyles != null)
        {
            root.Add(new XElement(masterStyles));
        }

        // 7. body 區段
        var bodyElement = contentRoot.Element(officeNs + "body");
        if (bodyElement != null)
        {
            root.Add(new XElement(bodyElement));
        }

        // 從虛擬項目中重新嵌入 Base64 圖片與子文件
        var xlinkNs = XNamespace.Get(OdfNamespaces.XLink);
        var elementsWithHref = root.Descendants().Where(e => e.Attribute(xlinkNs + "href") != null).ToList();

        foreach (var elem in elementsWithHref)
        {
            var hrefAttr = elem.Attribute(xlinkNs + "href")!;
            string href = hrefAttr.Value;
            if (href.StartsWith("Pictures/"))
            {
                if (_entries.TryGetValue(href, out var entry))
                {
                    byte[] imageBytes;
                    using (var entryReader = entry.OpenReader())
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
                if (_entries.TryGetValue(subDocContentPath, out var subDocEntry))
                {
                    string mimeType = "application/vnd.oasis.opendocument.formula";
                    string subDocMimePath = $"{normHref}/mimetype";
                    if (_entries.TryGetValue(subDocMimePath, out var mimeEntry))
                    {
                        using var mimeReader = new StreamReader(mimeEntry.OpenReader(), Encoding.UTF8);
                        mimeType = mimeReader.ReadToEnd().Trim();
                    }
                    else if (_manifest.TryGetValue(normHref, out var m))
                    {
                        mimeType = m;
                    }
                    else if (_manifest.TryGetValue(normHref + "/", out var mSlash))
                    {
                        mimeType = mSlash;
                    }

                    XElement subDocRoot;
                    using (var subReader = XmlReader.Create(subDocEntry.OpenReader(), xmlSettings))
                    {
                        subDocRoot = XDocument.Load(subReader).Root ?? throw new InvalidDataException($"Invalid {subDocContentPath} root");
                    }

                    var nestedDoc = new XElement(officeNs + "document");
                    nestedDoc.SetAttributeValue(officeNs + "mimetype", mimeType);

                    string subDocVersion = subDocRoot.Attribute(officeNs + "version")?.Value ?? "1.3";
                    nestedDoc.SetAttributeValue(officeNs + "version", subDocVersion);

                    CopyNamespaces(subDocRoot, nestedDoc);

                    foreach (var child in subDocRoot.Elements())
                    {
                        nestedDoc.Add(new XElement(child));
                    }

                    elem.Add(nestedDoc);

                    hrefAttr.Remove();
                    elem.Attribute(xlinkNs + "type")?.Remove();
                    elem.Attribute(xlinkNs + "show")?.Remove();
                    elem.Attribute(xlinkNs + "actuate")?.Remove();
                }
            }
        }

        // 將合併後的 XML 樹寫入 targetStream
        var writerSettings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = _saveOptions.IndentXml
        };
        using (var writer = XmlWriter.Create(targetStream, writerSettings))
        {
            root.Save(writer);
        }
    }

    #endregion
}
