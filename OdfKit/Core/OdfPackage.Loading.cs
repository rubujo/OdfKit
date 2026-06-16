using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
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


    private void InitializeLoad()
    {
        if (_underlyingStream == null)
            throw new InvalidOperationException("No input stream available.");

        // 嗅探簽章：檢查是否為 ZIP（PK\x03\x04）
        byte[] signature = new byte[4];
        int bytesRead = 0;
        if (_underlyingStream.CanSeek)
        {
            long initialPosition = _underlyingStream.Position;
            _underlyingStream.Position = 0;
            bytesRead = ReadAll(_underlyingStream, signature, 0, signature.Length);
            _underlyingStream.Position = initialPosition;
        }
        else
        {
            bytesRead = ReadAll(_underlyingStream, signature, 0, signature.Length);
        }

        bool isZip = bytesRead == 4 &&
                     signature[0] == 0x50 &&
                     signature[1] == 0x4B &&
                     signature[2] == 0x03 &&
                     signature[3] == 0x04;

        if (!isZip)
        {
            _isFlatXml = true;
            InitializeFlatXml(signature, bytesRead);
            return;
        }

        if (!_underlyingStream.CanSeek)
        {
            // 若為 ZIP 且串流不可搜尋，複製到可搜尋的 MemoryStream
            // 因為 ZipArchive 需要可搜尋串流才能讀取中央目錄
            var ms = new MemoryStream();
            ms.Write(signature, 0, bytesRead);
            _underlyingStream.CopyTo(ms);
            ms.Position = 0;
            if (!_leaveOpen)
            {
                _underlyingStream.Dispose();
            }
            _underlyingStream = ms;
        }

        // 若需要，在 .NET Standard 2.0 註冊 ZIP 檔名的 CodePages
#if NETSTANDARD2_0
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
            catch
            {
                // 若平台不支援或缺少參考則靜默略過
            }
#endif

        // 開啟 ZIP 封存
        _archive = new ZipArchive(_underlyingStream, ZipArchiveMode.Read, _leaveOpen, Encoding.UTF8);

        // Zip DoS 防禦：計算項目數量
        if (_archive.Entries.Count > _loadOptions.MaxZipEntries)
        {
            throw new SecurityException($"Zip archive contains too many entries ({_archive.Entries.Count} > {_loadOptions.MaxZipEntries}). Potential Zip DoS attack.");
        }

        long totalUncompressedSize = 0;

        foreach (var entry in _archive.Entries)
        {
            // 清理並檢查 Zip Slip。若不安全，仍以原始名稱存入記憶體
            // 以便合規驗證器回報（Fatal ODF0200/ODF0201 問題），
            // 但之後透過 SanitizeEntryName 存取此項目將拋出 SecurityException
            string name;
            try
            {
                name = SanitizeEntryName(entry.FullName);
            }
            catch (SecurityException)
            {
                name = entry.FullName;
            }

            // Zip DoS 防禦：項目大小
            if (entry.Length > _loadOptions.MaxEntrySize)
            {
                throw new SecurityException($"Zip entry '{name}' exceeds size limit ({entry.Length} > {_loadOptions.MaxEntrySize} bytes).");
            }

            totalUncompressedSize += entry.Length;
            if (totalUncompressedSize > _loadOptions.MaxTotalUncompressedSize)
            {
                throw new SecurityException($"Zip archive total uncompressed size exceeds limit ({totalUncompressedSize} > {_loadOptions.MaxTotalUncompressedSize} bytes).");
            }

            byte[] entryBytes;
            using (var entryStream = entry.Open())
            using (var ms = new MemoryStream())
            {
                entryStream.CopyTo(ms);
                entryBytes = ms.ToArray();
            }
            var pkgEntry = new OdfPackageEntry(name, entryBytes);
            if (entry.CompressedLength == entry.Length && entry.Length > 0)
            {
                pkgEntry.IsCompressed = false;
            }

            // 判斷是否以未壓縮方式儲存
            bool wasStored = false;
            try
            {
                var fieldInfo = typeof(ZipArchiveEntry).GetField("_compressionMethod", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?? typeof(ZipArchiveEntry).GetField("m_compressionMethod", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    var val = fieldInfo.GetValue(entry);
                    if (val != null)
                    {
                        int intVal = Convert.ToInt32(val);
                        wasStored = (intVal == 0);
                    }
                }
                else
                {
                    OdfKitDiagnostics.Warn($"[OdfPackage] 無法反射取得 ZipArchiveEntry 壓縮方式欄位 ( .NET {Environment.Version} )；讀取時將以 CompressedLength == Length 作為判斷基準。");
                    wasStored = (entry.CompressedLength == entry.Length);
                }
            }
            catch
            {
                wasStored = (entry.CompressedLength == entry.Length);
            }
            pkgEntry.WasStoredInZip = wasStored;
            if (_entries.ContainsKey(name))
            {
                _duplicateEntryNames.Add(name);
            }
            _entries[name] = pkgEntry;
            if (!_entryOrder.Contains(name))
            {
                _entryOrder.Add(name);
            }
        }

        // 載入 mimetype
        if (_entries.TryGetValue("mimetype", out var mimeEntry))
        {
            using var reader = new StreamReader(mimeEntry.OpenReader(), Encoding.UTF8);
            _mimetype = reader.ReadToEnd().Trim();
        }
        else if (_loadOptions.ValidateMimeType)
        {
            throw new InvalidDataException("Invalid ODF package: 'mimetype' file is missing.");
        }

        // 載入 manifest
        LoadManifest();

        if (_loadOptions.Password != null || _loadOptions.CryptographyProvider != null)
        {
            OdfEncryption.Decrypt(this, _loadOptions.Password ?? string.Empty);
        }

        LoadRdfMetadata();
    }

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

    private void LoadManifest()
    {
        if (!_entries.TryGetValue("META-INF/manifest.xml", out var manifestEntry))
        {
            if (_loadOptions.ValidateMimeType)
            {
                throw new InvalidDataException("Invalid ODF package: 'META-INF/manifest.xml' is missing.");
            }
            return;
        }

        using var stream = manifestEntry.OpenReader();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreWhitespace = true,
            MaxCharactersInDocument = _loadOptions.MaxXmlCharactersInDocument > 0 ? _loadOptions.MaxXmlCharactersInDocument : 0
        };

        OdfPackageEntry? currentEntry = null;
        OdfEncryptionInfo? currentEncryptionInfo = null;

        using var reader = XmlReader.Create(stream, settings);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.LocalName == "manifest" && reader.NamespaceURI == OdfNamespaces.Manifest)
                {
                    string? version = reader.GetAttribute("version", OdfNamespaces.Manifest) ?? reader.GetAttribute("version");
                    _manifestRootInfo = new OdfManifestRootInfo(reader.NamespaceURI, reader.LocalName, version);
                    if (version != null)
                    {
                        _version = version switch
                        {
                            "1.0" => OdfVersion.Odf10,
                            "1.1" => OdfVersion.Odf11,
                            "1.2" => OdfVersion.Odf12,
                            "1.3" => OdfVersion.Odf13,
                            "1.4" => OdfVersion.Odf14,
                            _ => OdfVersion.Odf14
                        };
                    }
                }
                else if (reader.LocalName == "file-entry" && reader.NamespaceURI == OdfNamespaces.Manifest)
                {
                    string? path = reader.GetAttribute("full-path", OdfNamespaces.Manifest) ?? reader.GetAttribute("full-path");
                    string? mediaType = reader.GetAttribute("media-type", OdfNamespaces.Manifest) ?? reader.GetAttribute("media-type");

                    var issue = new OdfManifestFileEntryIssue();
                    bool hasIssue = false;

                    if (path == null)
                    {
                        issue.MissingFullPath = true;
                        hasIssue = true;
                    }
                    else
                    {
                        issue.FullPath = path;
                        if (path != "/" && !IsSafeManifestPath(path))
                        {
                            issue.InvalidFullPath = true;
                            hasIssue = true;
                        }
                    }

                    if (mediaType == null)
                    {
                        issue.MissingMediaType = true;
                        hasIssue = true;
                    }

                    if (hasIssue)
                    {
                        _manifestFileEntryIssues.Add(issue);
                    }

                    if (path != null)
                    {
                        string normPath;
                        if (path == "/")
                        {
                            normPath = "/";
                        }
                        else if (!IsSafeManifestPath(path))
                        {
                            normPath = path;
                        }
                        else
                        {
                            try
                            {
                                normPath = SanitizeEntryName(path);
                            }
                            catch (SecurityException)
                            {
                                normPath = path;
                            }
                        }

                        if (_manifest.ContainsKey(normPath))
                        {
                            _duplicateManifestPaths.Add(normPath);
                        }
                        if (mediaType != null)
                        {
                            _manifest[normPath] = mediaType;
                        }

                        if (normPath != "/" && _entries.TryGetValue(normPath, out var entry))
                        {
                            currentEntry = entry;
                        }
                        else
                        {
                            currentEntry = null;
                        }
                    }
                    else
                    {
                        currentEntry = null;
                    }
                    currentEncryptionInfo = null;
                }
                else if (reader.LocalName == "encryption-data" && reader.NamespaceURI == OdfNamespaces.Manifest && currentEntry != null)
                {
                    currentEncryptionInfo = new OdfEncryptionInfo();
                    string? checksumType = reader.GetAttribute("checksum-type", OdfNamespaces.Manifest) ?? reader.GetAttribute("checksum-type");
                    string? checksumStr = reader.GetAttribute("checksum", OdfNamespaces.Manifest) ?? reader.GetAttribute("checksum");

                    if (!string.IsNullOrEmpty(checksumType))
                    {
                        currentEncryptionInfo.ChecksumType = checksumType;
                    }
                    if (!string.IsNullOrEmpty(checksumStr))
                    {
                        currentEncryptionInfo.Checksum = Convert.FromBase64String(checksumStr);
                    }

                    currentEntry.EncryptionInfo = currentEncryptionInfo;

                    // 將其他屬性載入 ExtensionProperties
                    for (int i = 0; i < reader.AttributeCount; i++)
                    {
                        reader.MoveToAttribute(i);
                        if (reader.LocalName != "checksum-type" && reader.LocalName != "checksum")
                        {
                            currentEncryptionInfo.ExtensionProperties[reader.LocalName] = reader.Value;
                        }
                    }
                    reader.MoveToElement();
                }
                else if (reader.LocalName == "algorithm" && reader.NamespaceURI == OdfNamespaces.Manifest && currentEncryptionInfo != null)
                {
                    string? algorithmName = reader.GetAttribute("algorithm-name", OdfNamespaces.Manifest) ?? reader.GetAttribute("algorithm-name");
                    string? ivStr = reader.GetAttribute("initialisation-vector", OdfNamespaces.Manifest) ?? reader.GetAttribute("initialisation-vector");

                    if (!string.IsNullOrEmpty(algorithmName))
                    {
                        currentEncryptionInfo.AlgorithmName = algorithmName;
                    }
                    if (!string.IsNullOrEmpty(ivStr))
                    {
                        currentEncryptionInfo.InitialisationVector = Convert.FromBase64String(ivStr);
                    }
                }
                else if (reader.LocalName == "key-derivation" && reader.NamespaceURI == OdfNamespaces.Manifest && currentEncryptionInfo != null)
                {
                    string? derivationName = reader.GetAttribute("key-derivation-name", OdfNamespaces.Manifest) ?? reader.GetAttribute("key-derivation-name");
                    string? keySizeStr = reader.GetAttribute("key-size", OdfNamespaces.Manifest) ?? reader.GetAttribute("key-size");
                    string? iterationCountStr = reader.GetAttribute("iteration-count", OdfNamespaces.Manifest) ?? reader.GetAttribute("iteration-count");
                    string? saltStr = reader.GetAttribute("salt", OdfNamespaces.Manifest) ?? reader.GetAttribute("salt");

                    if (!string.IsNullOrEmpty(derivationName))
                    {
                        currentEncryptionInfo.KeyDerivationName = derivationName;
                    }
                    if (int.TryParse(keySizeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int keySize))
                    {
                        currentEncryptionInfo.KeySize = keySize;
                    }
                    if (int.TryParse(iterationCountStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int iterationCount))
                    {
                        if (iterationCount > 50000)
                        {
                            throw new CryptographicException($"PBKDF2 iteration count {iterationCount} exceeds the maximum limit of 50000.");
                        }
                        currentEncryptionInfo.IterationCount = iterationCount;
                    }
                    if (!string.IsNullOrEmpty(saltStr))
                    {
                        currentEncryptionInfo.Salt = Convert.FromBase64String(saltStr);
                    }

                    // 讀取 key-derivation 節點的所有其它擴充屬性
                    for (int i = 0; i < reader.AttributeCount; i++)
                    {
                        reader.MoveToAttribute(i);
                        if (reader.LocalName is not "key-derivation-name" and not "key-size" and not "iteration-count" and not "salt")
                        {
                            currentEncryptionInfo.ExtensionProperties[reader.LocalName] = reader.Value;
                        }
                    }
                    reader.MoveToElement();
                }
                else if (reader.LocalName == "encrypted-key" && reader.NamespaceURI == OdfNamespaces.Manifest && currentEncryptionInfo != null)
                {
                    var encryptedKey = new OdfOpenPgpEncryptedKeyInfo
                    {
                        KeyId = reader.GetAttribute("key-id", OdfNamespaces.Manifest) ?? reader.GetAttribute("key-id") ?? string.Empty,
                        Recipient = reader.GetAttribute("recipient", OdfNamespaces.Manifest) ?? reader.GetAttribute("recipient"),
                        AlgorithmName = reader.GetAttribute("algorithm-name", OdfNamespaces.Manifest) ?? reader.GetAttribute("algorithm-name") ?? string.Empty
                    };

                    for (int i = 0; i < reader.AttributeCount; i++)
                    {
                        reader.MoveToAttribute(i);
                        if (reader.NamespaceURI == OdfNamespaces.Manifest &&
                            reader.LocalName is not "key-id" and not "recipient" and not "algorithm-name")
                        {
                            encryptedKey.ExtensionProperties[reader.LocalName] = reader.Value;
                        }
                    }
                    reader.MoveToElement();

                    if (reader.IsEmptyElement)
                    {
                        currentEncryptionInfo.OpenPgpEncryptedKeys.Add(encryptedKey);
                        continue;
                    }

                    string keyPacket = reader.ReadElementContentAsString();
                    if (!string.IsNullOrWhiteSpace(keyPacket))
                    {
                        encryptedKey.KeyPacket = Convert.FromBase64String(keyPacket.Trim());
                    }
                    currentEncryptionInfo.OpenPgpEncryptedKeys.Add(encryptedKey);
                    continue;
                }
                else if (reader.LocalName == "start-key-generation" && reader.NamespaceURI == OdfNamespaces.Manifest && currentEncryptionInfo != null)
                {
                    string? startKeyGenName = reader.GetAttribute("start-key-generation-name", OdfNamespaces.Manifest) ?? reader.GetAttribute("start-key-generation-name");
                    string? startKeySizeStr = reader.GetAttribute("key-size", OdfNamespaces.Manifest) ?? reader.GetAttribute("key-size");

                    if (!string.IsNullOrEmpty(startKeyGenName))
                    {
                        currentEncryptionInfo.StartKeyGenerationName = startKeyGenName;
                    }
                    if (int.TryParse(startKeySizeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int startKeySize))
                    {
                        currentEncryptionInfo.StartKeySize = startKeySize;
                    }
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                if (reader.LocalName == "file-entry" && reader.NamespaceURI == OdfNamespaces.Manifest)
                {
                    currentEntry = null;
                    currentEncryptionInfo = null;
                }
                else if (reader.LocalName == "encryption-data" && reader.NamespaceURI == OdfNamespaces.Manifest)
                {
                    currentEncryptionInfo = null;
                }
            }
        }
    }

    private bool IsSafeManifestPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.StartsWith("/", StringComparison.Ordinal) ||
            path.Contains("\\") ||
            path.Contains(":") ||
            path.Contains("//"))
        {
            return false;
        }

        string normalized = path.EndsWith("/", StringComparison.Ordinal)
            ? path.Substring(0, path.Length - 1)
            : path;

        if (normalized.Length == 0)
        {
            return false;
        }

        var parts = normalized.Split('/');
        foreach (var part in parts)
        {
            if (part.Length == 0 || part == "." || part == "..")
            {
                return false;
            }
        }

        return true;
    }


    #endregion
}
