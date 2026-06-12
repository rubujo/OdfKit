#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using OdfKit.DOM;
using OdfKit.Formula;
using OdfKit.Styles;

namespace OdfKit.Core
{
    public enum OdfPackageMode
    {
        Read,
        ReadWrite,
        Create
    }

    public class OdfPackage : IDisposable, IAsyncDisposable
    {
        private readonly OdfPackageMode _mode;
        private Stream? _underlyingStream;
        private readonly bool _leaveOpen;
        private readonly OdfLoadOptions _loadOptions;
        private readonly OdfSaveOptions _saveOptions;
        
        private ZipArchive? _archive;
        private readonly Dictionary<string, OdfPackageEntry> _entries = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _manifest = new(StringComparer.Ordinal);
        private readonly List<string> _entryOrder = new();
        private readonly List<string> _duplicateEntryNames = new();
        private readonly List<string> _duplicateManifestPaths = new();
        private readonly List<OdfManifestFileEntryIssue> _manifestFileEntryIssues = new();
        private OdfManifestRootInfo? _manifestRootInfo;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _isDisposed;
        private string? _mimetype;
        private bool _isFlatXml;

        public OdfPackageMode Mode => _mode;
        public string? MimeType => _mimetype;
        public bool IsFlatXml => _isFlatXml;
        public IReadOnlyDictionary<string, string> Manifest => _manifest;
        internal IReadOnlyDictionary<string, OdfPackageEntry> Entries => _entries;
        internal IReadOnlyList<string> EntryOrder => _entryOrder;
        internal IReadOnlyList<string> DuplicateEntryNames => _duplicateEntryNames;
        internal IReadOnlyList<string> DuplicateManifestPaths => _duplicateManifestPaths;
        internal IReadOnlyList<OdfManifestFileEntryIssue> ManifestFileEntryIssues => _manifestFileEntryIssues;
        internal OdfManifestRootInfo? ManifestRootInfo => _manifestRootInfo;
        internal OdfLoadOptions LoadOptions => _loadOptions;
        internal OdfSaveOptions SaveOptions => _saveOptions;

        public bool IsEntryEncrypted(string name)
        {
            name = SanitizeEntryName(name);
            return _entries.TryGetValue(name, out var entry) && entry.EncryptionInfo != null;
        }

        public OdfEncryptionInfo? GetEntryEncryptionInfo(string name)
        {
            name = SanitizeEntryName(name);
            return _entries.TryGetValue(name, out var entry) ? entry.EncryptionInfo : null;
        }

        private OdfPackage(OdfPackageMode mode, Stream? underlyingStream, bool leaveOpen, OdfLoadOptions? loadOptions, OdfSaveOptions? saveOptions)
        {
            _mode = mode;
            _underlyingStream = underlyingStream;
            _leaveOpen = leaveOpen;
            _loadOptions = loadOptions ?? OdfLoadOptions.Default;
            _saveOptions = saveOptions ?? OdfSaveOptions.Default;
        }

        #region Factory Methods

        public static OdfPackage Open(string path, OdfLoadOptions? options = null)
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var package = new OdfPackage(OdfPackageMode.ReadWrite, stream, false, options, null);
            try
            {
                package.InitializeLoad();
                return package;
            }
            catch
            {
                package.Dispose();
                throw;
            }
        }

        public static OdfPackage Open(Stream stream, bool leaveOpen = false, OdfLoadOptions? options = null)
        {
            var package = new OdfPackage(OdfPackageMode.ReadWrite, stream, leaveOpen, options, null);
            try
            {
                package.InitializeLoad();
                return package;
            }
            catch
            {
                if (!leaveOpen) stream.Dispose();
                throw;
            }
        }

        public static OdfPackage Create(string path, OdfSaveOptions? options = null)
        {
            var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            return new OdfPackage(OdfPackageMode.Create, stream, false, null, options);
        }

        public static OdfPackage Create(Stream stream, bool leaveOpen = false, OdfSaveOptions? options = null)
        {
            return new OdfPackage(OdfPackageMode.Create, stream, leaveOpen, null, options);
        }

        #endregion

        #region Initialization & Loading

        private void InitializeLoad()
        {
            if (_underlyingStream == null) throw new InvalidOperationException("No input stream available.");

            // Sniff signature: check if it is ZIP (PK\x03\x04)
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
                // If it is ZIP and non-seekable, we copy it to a seekable MemoryStream
                // because ZipArchive requires a seekable stream to read the central directory.
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

            // Register CodePages for ZIP filenames in .NET Standard 2.0 if needed
#if NETSTANDARD2_0
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
            catch
            {
                // Fallback silently if platform doesn't support or reference is missing
            }
#endif

            // Open ZIP archive
            _archive = new ZipArchive(_underlyingStream, ZipArchiveMode.Read, _leaveOpen, Encoding.UTF8);

            // Zip DoS Defense: count entries
            if (_archive.Entries.Count > _loadOptions.MaxZipEntries)
            {
                throw new SecurityException($"Zip archive contains too many entries ({_archive.Entries.Count} > {_loadOptions.MaxZipEntries}). Potential Zip DoS attack.");
            }

            long totalUncompressedSize = 0;

            foreach (var entry in _archive.Entries)
            {
                // Sanitize and check Zip Slip. If it is unsafe, we still store it in memory using the raw name
                // so that the compliance validator can report it (as a Fatal ODF0200/ODF0201 issue),
                // but any subsequent access to this entry via SanitizeEntryName will throw SecurityException.
                string name;
                try
                {
                    name = SanitizeEntryName(entry.FullName);
                }
                catch (SecurityException)
                {
                    name = entry.FullName;
                }

                // Zip DoS Defense: entry size
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
                
                // Determine if it was stored without compression
                bool wasStored = false;
                try
                {
                    var fieldInfo = typeof(ZipArchiveEntry).GetField("_compressionMethod", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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

            // Load mimetype
            if (_entries.TryGetValue("mimetype", out var mimeEntry))
            {
                using var reader = new StreamReader(mimeEntry.OpenReader(), Encoding.UTF8);
                _mimetype = reader.ReadToEnd().Trim();
            }
            else if (_loadOptions.ValidateMimeType)
            {
                throw new InvalidDataException("Invalid ODF package: 'mimetype' file is missing.");
            }

            // Load manifest
            LoadManifest();

            if (_loadOptions.Password != null || _loadOptions.CryptographyProvider != null)
            {
                OdfEncryption.Decrypt(this, _loadOptions.Password ?? string.Empty);
            }
        }

        private void InitializeFlatXml(byte[] signature, int signatureLength)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = !_leaveOpen
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

            // Get mimetype
            var mimeAttr = root.Attribute(officeNs + "mimetype") ?? root.Attribute("mimetype");
            _mimetype = mimeAttr?.Value;
            if (string.IsNullOrEmpty(_mimetype) && _loadOptions.ValidateMimeType)
            {
                throw new InvalidDataException("Invalid Flat XML: missing office:mimetype.");
            }

            // Get office:version
            var versionAttr = root.Attribute(officeNs + "version") ?? root.Attribute("version");
            string version = versionAttr?.Value ?? "1.3";

            // Extract nested office:document elements (embedded objects, e.g. formulas)
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

            // Extract office elements
            var metaElement = root.Element(officeNs + "meta");
            var settingsElement = root.Element(officeNs + "settings");
            var stylesElement = root.Element(officeNs + "styles");
            var autoStylesElement = root.Element(officeNs + "automatic-styles");
            var masterStylesElement = root.Element(officeNs + "master-styles");
            var fontDeclsElement = root.Element(officeNs + "font-face-decls");
            var bodyElement = root.Element(officeNs + "body");

            // Extract binary data (images)
            var binaryDataElements = doc.Descendants(officeNs + "binary-data").ToList();
            int imageCounter = 1;
            foreach (var binData in binaryDataElements)
            {
                string base64 = binData.Value;
                // Clean up whitespace/newlines from base64 string
                base64 = base64.Replace("\r", "").Replace("\n", "").Replace(" ", "").Replace("\t", "");
                byte[] bytes = Convert.FromBase64String(base64);

                // Detect image format/extension
                OdfMediaManager.DetectImageFormat(bytes, out var mediaType, out var ext);

                // Construct a virtual entry path, e.g. Pictures/image_1.png
                string imagePath = $"Pictures/image_{imageCounter++}{ext}";

                // Add to virtual entries
                _entries[imagePath] = new OdfPackageEntry(imagePath, bytes);
                _manifest[imagePath] = mediaType;
                _entryOrder.Add(imagePath);

                // Replace <office:binary-data> with drawing reference in parent
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

            // Construct content.xml
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

            // Construct styles.xml
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

            // Construct meta.xml
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

            // Construct settings.xml
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
                IgnoreWhitespace = true
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

                        // Load other attributes into ExtensionProperties
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

            var parts = path.Split('/');
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

        #region ZIP Path & Entry Sanitize (Zip Slip Protection)

        public static string SanitizeEntryName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            // Enforce strict directory traversal and malformed path defenses
            if (name.Contains(":") ||
                name.Contains("//") ||
                name.Contains(@"\\") ||
                name.Contains("../") ||
                name.Contains(@"..\") ||
                name.Equals("..") ||
                name.EndsWith("/..") ||
                name.EndsWith(@"\.."))
            {
                throw new SecurityException($"Forbidden absolute path, drive specifier, UNC format, double slashes, or directory traversal: {name}");
            }

            // Normalize backslashes to forward slashes
            string normalized = name.Replace('\\', '/');

            // Strip leading slashes
            while (normalized.StartsWith("/"))
            {
                normalized = normalized.Substring(1);
            }

            // Additional defense in split parts
            string[] parts = normalized.Split('/');
            foreach (var part in parts)
            {
                if (part == "..")
                {
                    throw new SecurityException($"Directory traversal attempt (Zip Slip) detected in entry name: {name}");
                }
            }

            return normalized;
        }

        #endregion

        #region Macro Sanitization

        /// <summary>
        /// Sanitizes the package by removing all VBA/StarBasic scripts, signatures, and script references.
        /// </summary>
        public void SanitizeMacros()
        {
            _lock.Wait();
            try
            {
                // 1. Collect and remove macro-related entries (basic/ folder, macrosignatures, etc.)
                var entriesToRemove = new List<string>();
                foreach (var key in _entries.Keys)
                {
                    if (key.StartsWith("basic/", StringComparison.OrdinalIgnoreCase) ||
                        key.StartsWith("Scripts/", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("macrosignatures.xml", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("META-INF/macrosignatures.xml", StringComparison.OrdinalIgnoreCase))
                    {
                        entriesToRemove.Add(key);
                    }
                }

                foreach (var key in entriesToRemove)
                {
                    _entries.Remove(key);
                    _manifest.Remove(key);
                    OdfKitDiagnostics.Info($"Removed macro or signature entry: {key}");
                }

                // 2. Iterate and sanitize all XML files inside the package (excluding META-INF/manifest.xml)
                var xmlEntries = new List<OdfPackageEntry>();
                foreach (var entry in _entries.Values)
                {
                    if (entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
                        !entry.Name.Equals("META-INF/manifest.xml", StringComparison.OrdinalIgnoreCase))
                    {
                        xmlEntries.Add(entry);
                    }
                }

                foreach (var entry in xmlEntries)
                {
                    try
                    {
                        OdfNode root;
                        using (var stream = entry.OpenReader())
                        {
                            root = OdfXmlReader.Parse(stream, _loadOptions);
                        }

                        if (SanitizeXmlNode(root))
                        {
                            using var ms = new MemoryStream();
                            OdfXmlWriter.Write(root, ms, _saveOptions);
                            
                            byte[] sanitizedBytes = ms.ToArray();
                            _entries[entry.Name] = new OdfPackageEntry(entry.Name, sanitizedBytes);
                            OdfKitDiagnostics.Info($"Sanitized macro references in XML entry: {entry.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        OdfKitDiagnostics.Warn($"Failed to sanitize XML entry '{entry.Name}': {ex.Message}");
                    }
                }

                // 3. Remove outdated document signatures
                RemoveOutdatedSignatures();
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Recursively sanitizes an XML node by removing event listeners and macro/script attributes.
        /// </summary>
        public static bool SanitizeXmlNode(OdfNode node)
        {
            if (node == null) return false;
            bool modified = false;

            // 1. Recursive removal of event-listener and script elements
            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                var child = node.Children[i];
                if (child.NodeType == OdfNodeType.Element)
                {
                    bool shouldRemove = false;

                    if ((child.LocalName == "event-listeners" && (child.NamespaceUri == OdfNamespaces.Office || child.NamespaceUri == OdfNamespaces.Presentation)) ||
                        (child.LocalName == "event-listener" && (child.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:script:1.0" || child.NamespaceUri == OdfNamespaces.Presentation)) ||
                        (child.LocalName == "script" && child.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:script:1.0") ||
                        (child.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:script:1.0"))
                    {
                        shouldRemove = true;
                    }

                    if (shouldRemove)
                    {
                        node.RemoveChild(child);
                        modified = true;
                    }
                    else
                    {
                        if (SanitizeXmlNode(child))
                        {
                            modified = true;
                        }
                    }
                }
            }

            // 2. Scan and sanitize attributes pointing to scripts/macros
            var attributesToRemove = new List<OdfAttributeName>();
            foreach (var attr in node.Attributes)
            {
                string val = attr.Value;
                bool isHref = attr.Key.LocalName == "href" && attr.Key.NamespaceUri == OdfNamespaces.XLink;
                if (val != null)
                {
                    if (val.IndexOf("ooo:StarBasic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        val.IndexOf("vnd.sun.star.script", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        val.IndexOf("vnd.sun.star.VBA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (isHref && (val.IndexOf("basic/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    val.IndexOf("basic\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    val.IndexOf("Scripts/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    val.IndexOf("Scripts\\", StringComparison.OrdinalIgnoreCase) >= 0)))
                    {
                        attributesToRemove.Add(attr.Key);
                    }
                }
            }

            foreach (var attrKey in attributesToRemove)
            {
                node.RemoveAttribute(attrKey.LocalName, attrKey.NamespaceUri);
                modified = true;
            }

            return modified;
        }

        #endregion

        #region Public API

        public bool HasEntry(string name)
        {
            return _entries.ContainsKey(SanitizeEntryName(name));
        }

        public class OdfPackageEntryInfo
        {
            public string Path { get; }
            public OdfPackageEntryInfo(string path) => Path = path;
        }

        public IEnumerable<OdfPackageEntryInfo> GetEntries()
        {
            return _entries.Keys.Select(k => new OdfPackageEntryInfo(k));
        }

        public byte[] ReadEntry(string path)
        {
            using var stream = GetEntryStream(path);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        public void Save(Stream stream)
        {
            SaveToStream(stream);
        }

        public Stream GetEntryStream(string name)
        {
            name = SanitizeEntryName(name);

            if (_entries.TryGetValue(name, out var entry))
            {
                return entry.OpenReader();
            }

            throw new FileNotFoundException($"Entry '{name}' not found in ODF package.");
        }

        public void WriteEntry(string name, byte[] content, string mediaType)
        {
            name = SanitizeEntryName(name);
            var entry = new OdfPackageEntry(name, content);
            _entries[name] = entry;
            _manifest[name] = mediaType;

            if (name.EndsWith("/mimetype") && name.Length > 9)
            {
                string folder = name.Substring(0, name.Length - 8); // keeps the trailing slash
                string mimeText = Encoding.UTF8.GetString(content).Trim();
                _manifest[folder] = mimeText;
            }

            // Clear signature on edit, except when writing signature itself or manifest
            if (name != "META-INF/documentsignatures.xml" && name != "META-INF/manifest.xml")
            {
                RemoveOutdatedSignatures();
            }
        }

        public void WriteEntry(string name, Stream contentStream, string mediaType)
        {
            name = SanitizeEntryName(name);
            var entry = new OdfPackageEntry(name, contentStream);
            _entries[name] = entry;
            _manifest[name] = mediaType;

            if (name.EndsWith("/mimetype") && name.Length > 9)
            {
                string folder = name.Substring(0, name.Length - 8); // keeps the trailing slash
                byte[] bytes;
                using (var ms = new MemoryStream())
                {
                    contentStream.CopyTo(ms);
                    bytes = ms.ToArray();
                }
                entry.SetContent(bytes);
                string mimeText = Encoding.UTF8.GetString(bytes).Trim();
                _manifest[folder] = mimeText;
            }

            if (name != "META-INF/documentsignatures.xml" && name != "META-INF/manifest.xml")
            {
                RemoveOutdatedSignatures();
            }
        }

        public void RemoveEntry(string name)
        {
            name = SanitizeEntryName(name);
            _entries.Remove(name);
            _manifest.Remove(name);

            if (name != "META-INF/documentsignatures.xml" && name != "META-INF/manifest.xml")
            {
                RemoveOutdatedSignatures();
            }
        }

        public void PruneUnusedMedia(IEnumerable<string> referencedMediaPaths)
        {
            var referencedSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in referencedMediaPaths)
            {
                referencedSet.Add(SanitizeEntryName(path));
            }

            var keysToRemove = new List<string>();
            foreach (var key in _entries.Keys)
            {
                if (key.StartsWith("Pictures/", StringComparison.OrdinalIgnoreCase))
                {
                    if (!referencedSet.Contains(key))
                    {
                        keysToRemove.Add(key);
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                RemoveEntry(key);
                OdfKitDiagnostics.Info($"Pruned unused media entry: {key}");
            }
        }

        public void SetMimeType(string mimetype)
        {
            _mimetype = mimetype;
            WriteEntry("mimetype", Encoding.UTF8.GetBytes(mimetype), string.Empty);
            // Mimetype itself does not compression
            if (_entries.TryGetValue("mimetype", out var mimeEntry))
            {
                mimeEntry.IsCompressed = false;
            }
        }

        #endregion

        #region Embedded Objects Extraction

        public IEnumerable<string> GetEmbeddedObjects()
        {
            var list = new List<string>();
            foreach (var kvp in _manifest)
            {
                // Embedded objects have media types starting with application/vnd.oasis.opendocument.*
                // and full paths that represent folders (registered in manifest with '/' at end or as parents)
                if (kvp.Key != "/" && kvp.Value.StartsWith("application/vnd.oasis.opendocument."))
                {
                    list.Add(kvp.Key);
                }
            }
            return list;
        }

        public Stream ExtractObjectStream(string objectName)
        {
            // Object streams are embedded folders. We look for entries inside objectName/
            // Usually, objects have content.xml, styles.xml, etc. inside their sub-paths.
            // If the user requests the object itself, we throw or return a sub-package.
            // For general embedding extraction, users can use GetEntryStream with objectName + "/content.xml".
            string path = SanitizeEntryName(objectName);
            return GetEntryStream(path + "/content.xml");
        }

        #endregion

        #region Saving and Atomic Save

        public void Save()
        {
            if (_mode == OdfPackageMode.Read)
            {
                throw new InvalidOperationException("Cannot save a read-only ODF package.");
            }

            _lock.Wait();
            try
            {
                // Process formulas and font embedding on save if configured
                ProcessSaveHooks();

                bool hasEncryption = _saveOptions.Password != null || _saveOptions.CryptographyProvider != null;
                if (hasEncryption)
                {
                    OdfEncryption.Encrypt(this, _saveOptions.Password ?? string.Empty, _saveOptions.EncryptionAlgorithm);
                }
                try
                {
                    // Write/update manifest before serialize
                    if (!_isFlatXml)
                    {
                        SaveManifestToEntries();
                    }

                    if (_underlyingStream != null && _underlyingStream.CanWrite)
                    {
                        long estimatedSize = 0;
                        foreach (var entry in _entries.Values)
                        {
                            estimatedSize += entry.GetEstimatedSize();
                        }

                        bool useTempFile = estimatedSize >= 50 * 1024 * 1024;
                        Stream tempStream;

                        if (useTempFile)
                        {
                            string tempDir = _saveOptions.TemporaryDirectory ?? Path.GetTempPath();
                            if (!Directory.Exists(tempDir))
                            {
                                Directory.CreateDirectory(tempDir);
                            }
                            string tempFilePath = Path.Combine(tempDir, "odfkit_" + Path.GetRandomFileName());
                            tempStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
                        }
                        else
                        {
                            tempStream = new MemoryStream();
                        }

                        try
                        {
                            WriteToArchive(tempStream);

                            _underlyingStream.SetLength(0);
                            tempStream.Position = 0;
                            tempStream.CopyTo(_underlyingStream);
                            _underlyingStream.Flush();
                        }
                        finally
                        {
                            tempStream.Dispose();
                        }
                    }
                }
                finally
                {
                    if (hasEncryption)
                    {
                        OdfEncryption.Decrypt(this, _saveOptions.Password ?? string.Empty);
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            if (_mode == OdfPackageMode.Read)
            {
                throw new InvalidOperationException("Cannot save a read-only ODF package.");
            }

            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Process formulas and font embedding on save if configured
                ProcessSaveHooks();

                bool hasEncryption = _saveOptions.Password != null || _saveOptions.CryptographyProvider != null;
                if (hasEncryption)
                {
                    OdfEncryption.Encrypt(this, _saveOptions.Password ?? string.Empty, _saveOptions.EncryptionAlgorithm);
                }
                try
                {
                    if (!_isFlatXml)
                    {
                        SaveManifestToEntries();
                    }

                    if (_underlyingStream != null && _underlyingStream.CanWrite)
                    {
                        long estimatedSize = 0;
                        foreach (var entry in _entries.Values)
                        {
                            estimatedSize += entry.GetEstimatedSize();
                        }

                        bool useTempFile = estimatedSize >= 50 * 1024 * 1024;
                        Stream tempStream;

                        if (useTempFile)
                        {
                            string tempDir = _saveOptions.TemporaryDirectory ?? Path.GetTempPath();
                            if (!Directory.Exists(tempDir))
                            {
                                Directory.CreateDirectory(tempDir);
                            }
                            string tempFilePath = Path.Combine(tempDir, "odfkit_" + Path.GetRandomFileName());
                            tempStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
                        }
                        else
                        {
                            tempStream = new MemoryStream();
                        }

                        try
                        {
                            await Task.Run(() => WriteToArchive(tempStream), cancellationToken).ConfigureAwait(false);

                            _underlyingStream.SetLength(0);
                            tempStream.Position = 0;
                            await tempStream.CopyToAsync(_underlyingStream, 81920, cancellationToken).ConfigureAwait(false);
                            await _underlyingStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            if (tempStream is IAsyncDisposable asyncTempStream)
                            {
                                await asyncTempStream.DisposeAsync().ConfigureAwait(false);
                            }
                            else
                            {
                                tempStream.Dispose();
                            }
                        }
                    }
                }
                finally
                {
                    if (hasEncryption)
                    {
                        OdfEncryption.Decrypt(this, _saveOptions.Password ?? string.Empty);
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public void SaveToStream(Stream destinationStream)
        {
            if (destinationStream == null) throw new ArgumentNullException(nameof(destinationStream));
            
            _lock.Wait();
            try
            {
                ProcessSaveHooks();

                bool hasEncryption = _saveOptions.Password != null || _saveOptions.CryptographyProvider != null;
                if (hasEncryption)
                {
                    OdfEncryption.Encrypt(this, _saveOptions.Password ?? string.Empty, _saveOptions.EncryptionAlgorithm);
                }
                try
                {
                    if (!_isFlatXml)
                    {
                        SaveManifestToEntries();
                    }
                    WriteToArchive(destinationStream);
                }
                finally
                {
                    if (hasEncryption)
                    {
                        OdfEncryption.Decrypt(this, _saveOptions.Password ?? string.Empty);
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SaveToStreamAsync(Stream destinationStream, CancellationToken cancellationToken = default)
        {
            if (destinationStream == null) throw new ArgumentNullException(nameof(destinationStream));
            
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ProcessSaveHooks();

                bool hasEncryption = _saveOptions.Password != null || _saveOptions.CryptographyProvider != null;
                if (hasEncryption)
                {
                    OdfEncryption.Encrypt(this, _saveOptions.Password ?? string.Empty, _saveOptions.EncryptionAlgorithm);
                }
                try
                {
                    if (!_isFlatXml)
                    {
                        SaveManifestToEntries();
                    }
                    await Task.Run(() => WriteToArchive(destinationStream), cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    if (hasEncryption)
                    {
                        OdfEncryption.Decrypt(this, _saveOptions.Password ?? string.Empty);
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        internal void SaveManifestToEntries()
        {
            // Build manifest.xml
            using var ms = new MemoryStream();
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false), // UTF-8 without BOM
                Indent = _saveOptions.IndentXml
            };

            using (var writer = XmlWriter.Create(ms, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("manifest", "manifest", OdfNamespaces.Manifest);
                writer.WriteAttributeString("manifest", "version", OdfNamespaces.Manifest, "1.3");

                // Root entry
                writer.WriteStartElement("file-entry", OdfNamespaces.Manifest);
                writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, "/");
                writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, _mimetype ?? "application/vnd.oasis.opendocument.text");
                writer.WriteEndElement();

                // Collect directory entries
                var directories = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var key in _manifest.Keys)
                {
                    int slashIdx = key.IndexOf('/');
                    if (slashIdx != -1)
                    {
                        string dir = key.Substring(0, slashIdx + 1);
                        if (!directories.ContainsKey(dir))
                        {
                            // Try to read mimetype for this directory
                            string mimeKey = dir + "mimetype";
                            string mimeType = "";
                            if (_entries.TryGetValue(mimeKey, out var mimeEntry))
                            {
                                try
                                {
                                    using var r = new StreamReader(mimeEntry.OpenReader(), Encoding.UTF8);
                                    mimeType = r.ReadToEnd().Trim();
                                }
                                catch {}
                            }
                            directories[dir] = mimeType;
                        }
                    }
                }

                // Rest of manifest sorted by key for deterministic output (required for digital signatures)
                var sortedKeys = new List<string>(_manifest.Keys);
                foreach (var dir in directories.Keys)
                {
                    if (!sortedKeys.Contains(dir))
                    {
                        sortedKeys.Add(dir);
                    }
                }
                sortedKeys.Sort(StringComparer.Ordinal);
                foreach (var key in sortedKeys)
                {
                    if (key == "/" || key == "mimetype" || key == "META-INF/manifest.xml") continue;

                    writer.WriteStartElement("file-entry", OdfNamespaces.Manifest);
                    writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, key);
                    
                    string mediaType = _manifest.TryGetValue(key, out var mt) ? mt : (directories.TryGetValue(key, out var dm) ? dm : "");
                    writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, mediaType);

                    if (_entries.TryGetValue(key, out var entry) && entry.EncryptionInfo != null)
                    {
                        var info = entry.EncryptionInfo;
                        writer.WriteStartElement("encryption-data", OdfNamespaces.Manifest);
                        writer.WriteAttributeString("manifest", "checksum-type", OdfNamespaces.Manifest, info.ChecksumType);
                        writer.WriteAttributeString("manifest", "checksum", OdfNamespaces.Manifest, Convert.ToBase64String(info.Checksum));

                        if (info.ExtensionProperties != null)
                        {
                            foreach (var prop in info.ExtensionProperties)
                            {
                                writer.WriteAttributeString("manifest", prop.Key, OdfNamespaces.Manifest, prop.Value);
                            }
                        }

                        writer.WriteStartElement("algorithm", OdfNamespaces.Manifest);
                        writer.WriteAttributeString("manifest", "algorithm-name", OdfNamespaces.Manifest, info.AlgorithmName);
                        writer.WriteAttributeString("manifest", "initialisation-vector", OdfNamespaces.Manifest, Convert.ToBase64String(info.InitialisationVector));
                        writer.WriteEndElement(); // algorithm

                        writer.WriteStartElement("key-derivation", OdfNamespaces.Manifest);
                        writer.WriteAttributeString("manifest", "key-derivation-name", OdfNamespaces.Manifest, info.KeyDerivationName);
                        writer.WriteAttributeString("manifest", "key-size", OdfNamespaces.Manifest, info.KeySize.ToString(CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("manifest", "iteration-count", OdfNamespaces.Manifest, info.IterationCount.ToString(CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("manifest", "salt", OdfNamespaces.Manifest, Convert.ToBase64String(info.Salt));
                        writer.WriteEndElement(); // key-derivation

                        if (!string.IsNullOrEmpty(info.StartKeyGenerationName) && info.StartKeySize.HasValue)
                        {
                            writer.WriteStartElement("start-key-generation", OdfNamespaces.Manifest);
                            writer.WriteAttributeString("manifest", "start-key-generation-name", OdfNamespaces.Manifest, info.StartKeyGenerationName);
                            writer.WriteAttributeString("manifest", "key-size", OdfNamespaces.Manifest, info.StartKeySize.Value.ToString(CultureInfo.InvariantCulture));
                            writer.WriteEndElement(); // start-key-generation
                        }

                        writer.WriteEndElement(); // encryption-data
                    }

                    writer.WriteEndElement(); // file-entry
                }

                writer.WriteEndElement(); // manifest
                writer.WriteEndDocument();
            }

            // WriteEntry handles signature removal; temporarily bypass it during internal manifest generation
            var manifestEntryName = "META-INF/manifest.xml";
            var pkgEntry = new OdfPackageEntry(manifestEntryName, ms.ToArray());
            _entries[manifestEntryName] = pkgEntry;
            _manifest[manifestEntryName] = "text/xml";
        }

        private void RemoveOutdatedSignatures()
        {
            // Invalid digital signatures are automatically cleared upon modification to prevent warnings
            if (HasEntry("META-INF/documentsignatures.xml"))
            {
                // Temporarily bypass WriteEntry/RemoveEntry to avoid infinite recursion
                _entries.Remove("META-INF/documentsignatures.xml");
                _manifest.Remove("META-INF/documentsignatures.xml");
                OdfKitDiagnostics.Info("Outdated digital signatures removed due to package edit.");
            }
        }

        private void ProcessSaveHooks()
        {
            bool evaluateFormulas = _saveOptions.EvaluateFormulasOnSave;
            bool embedFonts = _saveOptions.EmbedUsedFonts;

            if (!evaluateFormulas && !embedFonts)
            {
                return;
            }

            OdfNode? contentRoot = null;
            OdfNode? stylesRoot = null;

            if (_entries.TryGetValue("content.xml", out var contentEntry))
            {
                try
                {
                    using var stream = contentEntry.OpenReader();
                    contentRoot = OdfXmlReader.Parse(stream, _loadOptions);
                }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Warn($"Failed to parse content.xml for save processing: {ex.Message}");
                }
            }

            if (embedFonts && _entries.TryGetValue("styles.xml", out var stylesEntry))
            {
                try
                {
                    using var stream = stylesEntry.OpenReader();
                    stylesRoot = OdfXmlReader.Parse(stream, _loadOptions);
                }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Warn($"Failed to parse styles.xml for save processing: {ex.Message}");
                }
            }

            bool contentModified = false;
            bool stylesModified = false;

            if (evaluateFormulas && contentRoot != null)
            {
                try
                {
                    var evaluator = new DefaultFormulaEvaluator();
                    evaluator.EvaluateFormulasInDocument(contentRoot);
                    contentModified = true;
                }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Warn($"Failed to evaluate formulas in document on save: {ex.Message}");
                }
            }

            if (embedFonts && (contentRoot != null || stylesRoot != null))
            {
                try
                {
                    var dummy = new OdfNode(OdfNodeType.Element, "dummy", string.Empty);
                    OdfFontResolver.EmbedFonts(this, contentRoot ?? dummy, stylesRoot ?? dummy);
                    if (contentRoot != null) contentModified = true;
                    if (stylesRoot != null) stylesModified = true;
                }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Warn($"Failed to embed fonts in document on save: {ex.Message}");
                }
            }

            if (contentModified && contentRoot != null)
            {
                try
                {
                    using var ms = new MemoryStream();
                    OdfXmlWriter.Write(contentRoot, ms, _saveOptions);
                    WriteEntry("content.xml", ms.ToArray(), "text/xml");
                }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Error($"Failed to write updated content.xml back to package on save: {ex.Message}", ex);
                }
            }

            if (stylesModified && stylesRoot != null)
            {
                try
                {
                    using var ms = new MemoryStream();
                    OdfXmlWriter.Write(stylesRoot, ms, _saveOptions);
                    WriteEntry("styles.xml", ms.ToArray(), "text/xml");
                }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Error($"Failed to write updated styles.xml back to package on save: {ex.Message}", ex);
                }
            }
        }

        private void WriteToArchive(Stream targetStream)
        {
            if (_isFlatXml)
            {
                WriteFlatXmlToStream(targetStream);
                return;
            }

            using var zip = new ZipArchive(targetStream, ZipArchiveMode.Create, true, Encoding.UTF8);

            // 1. mimetype MUST be first and Stored (uncompressed)
            if (_entries.TryGetValue("mimetype", out var mimeEntry))
            {
                var zipEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
                
                // Write fixed timestamp if Determinional is enabled
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

            // 2. Write all other entries
            foreach (var kvp in _entries)
            {
                if (kvp.Key == "mimetype") continue;

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
                XmlResolver = null
            };

            // Read content.xml
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

            // Read styles.xml
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

            // Read meta.xml
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

            // Read settings.xml
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

            // Construct new office:document
            var root = new XElement(officeNs + "document");

            // Copy version and mimetype
            string version = contentRoot.Attribute(officeNs + "version")?.Value ?? "1.3";
            root.SetAttributeValue(officeNs + "version", version);
            if (!string.IsNullOrEmpty(_mimetype))
            {
                root.SetAttributeValue(officeNs + "mimetype", _mimetype);
            }

            // Copy namespace declarations
            CopyNamespaces(contentRoot, root);
            CopyNamespaces(stylesRoot, root);
            CopyNamespaces(metaRoot, root);
            CopyNamespaces(settingsRoot, root);

            // 1. meta
            var metaElement = metaRoot.Element(officeNs + "meta");
            if (metaElement != null)
            {
                root.Add(new XElement(metaElement));
            }

            // 2. settings
            var settingsElement = settingsRoot.Element(officeNs + "settings");
            if (settingsElement != null)
            {
                root.Add(new XElement(settingsElement));
            }

            // 3. font-face-decls
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

            // 4. styles
            var stylesElement = stylesRoot.Element(officeNs + "styles");
            if (stylesElement != null)
            {
                root.Add(new XElement(stylesElement));
            }

            // 5. automatic-styles
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
                        if (existing != null) continue;
                    }
                    combinedAutoStyles.Add(new XElement(element));
                }
            }
            if (combinedAutoStyles.HasElements)
            {
                root.Add(combinedAutoStyles);
            }

            // 6. master-styles
            var masterStyles = stylesRoot.Element(officeNs + "master-styles");
            if (masterStyles != null)
            {
                root.Add(new XElement(masterStyles));
            }

            // 7. body
            var bodyElement = contentRoot.Element(officeNs + "body");
            if (bodyElement != null)
            {
                root.Add(new XElement(bodyElement));
            }

            // Re-embed base64 images and sub-documents from virtual entries
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

            // Write consolidated XML tree to targetStream
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

        #region Dispose

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _lock.Dispose();
                    _archive?.Dispose();
                    if (!_leaveOpen)
                    {
                        _underlyingStream?.Dispose();
                    }

                    // Dispose loaded entry streams
                    foreach (var entry in _entries.Values)
                    {
                        entry.Dispose();
                    }
                }
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_isDisposed)
            {
                _lock.Dispose();
                _archive?.Dispose();

                if (!_leaveOpen && _underlyingStream != null)
                {
                    if (_underlyingStream is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        _underlyingStream.Dispose();
                    }
                }

                foreach (var entry in _entries.Values)
                {
                    entry.Dispose();
                }

                _isDisposed = true;
            }
            GC.SuppressFinalize(this);
        }

        private static int ReadAll(Stream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (read <= 0) break;
                totalRead += read;
            }
            return totalRead;
        }

        #endregion
    }

    #region Package Entry Representation

    internal class OdfPackageEntry : IDisposable
    {
        public string Name { get; }
        private readonly ZipArchiveEntry? _zipEntry;
        private byte[]? _bytes;
        private Stream? _stream;
        public bool IsCompressed { get; set; } = true;
        public OdfEncryptionInfo? EncryptionInfo { get; set; }

        private bool? _wasStoredInZip;
        public bool WasStoredInZip
        {
            get
            {
                if (_wasStoredInZip.HasValue) return _wasStoredInZip.Value;
                if (_zipEntry == null)
                {
                    return !IsCompressed;
                }
                try
                {
                    var fieldInfo = typeof(ZipArchiveEntry).GetField("_compressionMethod", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (fieldInfo != null)
                    {
                        var val = fieldInfo.GetValue(_zipEntry);
                        if (val != null)
                        {
                            int intVal = Convert.ToInt32(val);
                            return intVal == 0; // 0 is Stored
                        }
                    }
                }
                catch
                {
                    // Fallback
                }
                return _zipEntry.CompressedLength == _zipEntry.Length;
            }
            internal set => _wasStoredInZip = value;
        }

        public void SetContent(byte[] bytes)
        {
            _bytes = bytes;
            _stream = null;
        }

        public long GetEstimatedSize()
        {
            if (_bytes != null) return _bytes.Length;
            if (_stream != null && _stream.CanSeek) return _stream.Length;
            if (_zipEntry != null) return _zipEntry.Length;
            return 0;
        }

        public OdfPackageEntry(string name, ZipArchiveEntry zipEntry)
        {
            Name = name;
            _zipEntry = zipEntry;
        }

        public OdfPackageEntry(string name, byte[] bytes)
        {
            Name = name;
            _bytes = bytes;
        }

        public OdfPackageEntry(string name, Stream stream)
        {
            Name = name;
            _stream = stream;
        }

        public Stream OpenReader()
        {
            if (_bytes != null)
            {
                return new MemoryStream(_bytes, false);
            }

            if (_stream != null)
            {
                if (_stream.CanSeek)
                {
                    _stream.Position = 0;
                }
                return _stream;
            }

            if (_zipEntry != null)
            {
                // In ZipArchiveMode.Read, we can call Open() multiple times but it will return a new stream.
                // We cache it in memory to support multiple reads or if archive gets closed.
                using var zipStream = _zipEntry.Open();
                var ms = new MemoryStream();
                zipStream.CopyTo(ms);
                _bytes = ms.ToArray();
                return new MemoryStream(_bytes, false);
            }

            throw new InvalidOperationException("OdfPackageEntry is in an invalid state.");
        }

        public void Dispose()
        {
            _stream?.Dispose();
        }
    }

    internal class PeekableStream : Stream
    {
        private readonly Stream _underlying;
        private readonly byte[] _peekBuffer;
        private readonly int _peekedCount;
        private readonly bool _leaveOpen;
        private int _peekPosition;

        public PeekableStream(Stream underlying, byte[] peekBuffer, int peekedCount, bool leaveOpen)
        {
            _underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
            _peekBuffer = peekBuffer ?? throw new ArgumentNullException(nameof(peekBuffer));
            _peekedCount = peekedCount;
            _leaveOpen = leaveOpen;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _underlying.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;
            if (_peekPosition < _peekedCount)
            {
                int available = _peekedCount - _peekPosition;
                int toCopy = Math.Min(available, count);
                Array.Copy(_peekBuffer, _peekPosition, buffer, offset, toCopy);
                _peekPosition += toCopy;
                offset += toCopy;
                count -= toCopy;
                bytesRead += toCopy;
            }

            if (count > 0)
            {
                bytesRead += _underlying.Read(buffer, offset, count);
            }

            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveOpen)
            {
                _underlying.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #endregion
}
