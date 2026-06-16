using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    #region Initialization & Loading

    private void InitializeFlatXml(byte[] signature, int signatureLength)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            CloseInput = !_leaveOpen,
            MaxCharactersInDocument = _loadOptions.MaxXmlCharactersInDocument > 0 ? _loadOptions.MaxXmlCharactersInDocument : 0
        };

        XDocument doc;
        Stream xmlStream = _underlyingStream!;
        if (!_underlyingStream!.CanSeek && signatureLength > 0)
        {
            xmlStream = new PeekableStream(_underlyingStream, signature, signatureLength, _leaveOpen);
        }

        using (var reader = XmlReader.Create(xmlStream, settings))
        {
            doc = XDocument.Load(reader);
        }

        var root = doc.Root;
        if (root == null || root.Name.LocalName != "document" || root.Name.NamespaceName != OdfNamespaces.Office)
        {
            throw new InvalidDataException("Invalid Flat XML: root element must be office:document.");
        }

        var officeNs = XNamespace.Get(OdfNamespaces.Office);
        var xlinkNs = XNamespace.Get(OdfNamespaces.XLink);

        // 取得 mimetype
        var mimeAttr = root.Attribute(officeNs + "mimetype") ?? root.Attribute("mimetype");
        _mimetype = mimeAttr?.Value;
        if (string.IsNullOrEmpty(_mimetype) && _loadOptions.ValidateMimeType)
        {
            throw new InvalidDataException("Invalid Flat XML: missing office:mimetype.");
        }

        // 取得 office:version
        var versionAttr = root.Attribute(officeNs + "version") ?? root.Attribute("version");
        string version = versionAttr?.Value ?? "1.3";
        _version = version switch
        {
            "1.0" => OdfVersion.Odf10,
            "1.1" => OdfVersion.Odf11,
            "1.2" => OdfVersion.Odf12,
            "1.3" => OdfVersion.Odf13,
            "1.4" => OdfVersion.Odf14,
            _ => OdfVersion.Odf14
        };

        // 擷取巢狀 office:document 元素（內嵌物件，例如公式）
        var nestedDocs = doc.Descendants(officeNs + "document")
                            .Where(d => d != doc.Root)
                            .ToList();

        int objectCounter = 1;
        foreach (var nestedDoc in nestedDocs)
        {
            var parent = nestedDoc.Parent;
            if (parent != null && parent.Name.LocalName == "object" && parent.Name.NamespaceName == OdfNamespaces.Draw)
            {
                string mimeType = nestedDoc.Attribute(officeNs + "mimetype")?.Value
                                  ?? nestedDoc.Attribute("mimetype")?.Value
                                  ?? "application/vnd.oasis.opendocument.formula";

                string? objectId = parent.Attribute(XNamespace.Get(OdfNamespaces.XLink) + "href")?.Value;
                if (string.IsNullOrEmpty(objectId))
                {
                    objectId = $"Object_{objectCounter++}";
                }
                else
                {
                    objectId = objectId!.TrimStart('.', '/').TrimEnd('/');
                }
                string subDocVersion = nestedDoc.Attribute(officeNs + "version")?.Value ?? "1.3";

                var subDocRoot = new XElement(officeNs + "document-content",
                    new XAttribute(officeNs + "version", subDocVersion));

                CopyNamespaces(nestedDoc, subDocRoot);
                foreach (var child in nestedDoc.Elements())
                {
                    subDocRoot.Add(new XElement(child));
                }

                byte[] contentBytes;
                using (var ms = new MemoryStream())
                {
                    var xdoc = new XDocument(subDocRoot);
                    xdoc.Save(ms);
                    contentBytes = ms.ToArray();
                }

                string folderPath = objectId;
                string contentPath = $"{folderPath}/content.xml";
                string mimePath = $"{folderPath}/mimetype";

                _entries[contentPath] = new OdfPackageEntry(contentPath, contentBytes);
                _manifest[contentPath] = "text/xml";
                _entryOrder.Add(contentPath);

                byte[] mimeBytes = Encoding.UTF8.GetBytes(mimeType);
                _entries[mimePath] = new OdfPackageEntry(mimePath, mimeBytes);
                _manifest[mimePath] = mimeType;
                _entryOrder.Add(mimePath);

                _manifest[folderPath + "/"] = mimeType;

                nestedDoc.Remove();

                var xlinkNsFormula = XNamespace.Get(OdfNamespaces.XLink);
                parent.SetAttributeValue(xlinkNsFormula + "href", folderPath);
                parent.SetAttributeValue(xlinkNsFormula + "type", "simple");
                parent.SetAttributeValue(xlinkNsFormula + "show", "embed");
                parent.SetAttributeValue(xlinkNsFormula + "actuate", "onLoad");
            }
        }

        // 擷取 office 元素
        var metaElement = root.Element(officeNs + "meta");
        var settingsElement = root.Element(officeNs + "settings");
        var stylesElement = root.Element(officeNs + "styles");
        var autoStylesElement = root.Element(officeNs + "automatic-styles");
        var masterStylesElement = root.Element(officeNs + "master-styles");
        var fontDeclsElement = root.Element(officeNs + "font-face-decls");
        var bodyElement = root.Element(officeNs + "body");

        // 擷取二進位資料（圖片）
        var binaryDataElements = doc.Descendants(officeNs + "binary-data").ToList();
        int imageCounter = 1;
        foreach (var binData in binaryDataElements)
        {
            string base64 = binData.Value;
            // 清除 Base64 字串中的空白與換行
            base64 = base64.Replace("\r", "").Replace("\n", "").Replace(" ", "").Replace("\t", "");
            byte[] bytes = Convert.FromBase64String(base64);

            // 偵測圖片格式與副檔名
            OdfMediaManager.DetectImageFormat(bytes, out var mediaType, out var ext);

            // 建構虛擬項目路徑，例如 Pictures/image_1.png
            string imagePath = $"Pictures/image_{imageCounter++}{ext}";

            // 加入虛擬項目
            _entries[imagePath] = new OdfPackageEntry(imagePath, bytes);
            _manifest[imagePath] = mediaType;
            _entryOrder.Add(imagePath);

            // 在父節點中以繪圖參照取代 <office:binary-data>
            var parent = binData.Parent;
            if (parent != null)
            {
                binData.Remove();

                xlinkNs = XNamespace.Get(OdfNamespaces.XLink);

                parent.SetAttributeValue(xlinkNs + "href", imagePath);
                parent.SetAttributeValue(xlinkNs + "type", "simple");
                parent.SetAttributeValue(xlinkNs + "show", "embed");
                parent.SetAttributeValue(xlinkNs + "actuate", "onLoad");
            }
        }

        // 建構 content.xml
        var contentRoot = new XElement(officeNs + "document-content",
            new XAttribute(officeNs + "version", version));
        CopyNamespaces(root, contentRoot);

        if (fontDeclsElement != null)
        {
            contentRoot.Add(new XElement(fontDeclsElement));
        }
        if (autoStylesElement != null)
        {
            contentRoot.Add(new XElement(autoStylesElement));
        }
        if (bodyElement != null)
        {
            contentRoot.Add(new XElement(bodyElement));
        }

        // 建構 styles.xml
        var stylesRoot = new XElement(officeNs + "document-styles",
            new XAttribute(officeNs + "version", version));
        CopyNamespaces(root, stylesRoot);

        if (fontDeclsElement != null)
        {
            stylesRoot.Add(new XElement(fontDeclsElement));
        }
        if (stylesElement != null)
        {
            stylesRoot.Add(new XElement(stylesElement));
        }
        if (autoStylesElement != null)
        {
            stylesRoot.Add(new XElement(autoStylesElement));
        }
        if (masterStylesElement != null)
        {
            stylesRoot.Add(new XElement(masterStylesElement));
        }

        // 建構 meta.xml
        var metaRoot = new XElement(officeNs + "document-meta",
            new XAttribute(officeNs + "version", version));
        CopyNamespaces(root, metaRoot);

        if (metaElement != null)
        {
            metaRoot.Add(new XElement(metaElement));
        }
        else
        {
            metaRoot.Add(new XElement(officeNs + "meta"));
        }

        // 建構 settings.xml
        var settingsRoot = new XElement(officeNs + "document-settings",
            new XAttribute(officeNs + "version", version));
        CopyNamespaces(root, settingsRoot);

        if (settingsElement != null)
        {
            settingsRoot.Add(new XElement(settingsElement));
        }
        else
        {
            settingsRoot.Add(new XElement(officeNs + "settings"));
        }

        byte[] ToUtf8Bytes(XElement element)
        {
            using (var ms = new MemoryStream())
            {
                var writerSettings = new XmlWriterSettings
                {
                    Encoding = new UTF8Encoding(false),
                    Indent = _saveOptions.IndentXml
                };
                using (var writer = XmlWriter.Create(ms, writerSettings))
                {
                    element.Save(writer);
                }
                return ms.ToArray();
            }
        }

        WriteVirtualEntry("content.xml", ToUtf8Bytes(contentRoot), "text/xml");
        WriteVirtualEntry("styles.xml", ToUtf8Bytes(stylesRoot), "text/xml");
        WriteVirtualEntry("meta.xml", ToUtf8Bytes(metaRoot), "text/xml");
        WriteVirtualEntry("settings.xml", ToUtf8Bytes(settingsRoot), "text/xml");
        if (!string.IsNullOrEmpty(_mimetype))
        {
            WriteVirtualEntry("mimetype", Encoding.UTF8.GetBytes(_mimetype), string.Empty);
        }
    }

    private void WriteVirtualEntry(string name, byte[] content, string mediaType)
    {
        name = SanitizeEntryName(name);
        _entries[name] = new OdfPackageEntry(name, content);
        _manifest[name] = mediaType;
        if (!_entryOrder.Contains(name))
        {
            _entryOrder.Add(name);
        }
    }

    private void CopyNamespaces(XElement source, XElement target)
    {
        foreach (var attr in source.Attributes())
        {
            if (attr.IsNamespaceDeclaration)
            {
                if (target.Attribute(attr.Name) == null)
                {
                    target.SetAttributeValue(attr.Name, attr.Value);
                }
            }
        }
    }

    #endregion
}
