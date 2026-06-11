using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
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
        private readonly Stream? _underlyingStream;
        private readonly bool _leaveOpen;
        private readonly OdfLoadOptions _loadOptions;
        private readonly OdfSaveOptions _saveOptions;
        
        private ZipArchive? _archive;
        private readonly Dictionary<string, OdfPackageEntry> _entries = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _manifest = new(StringComparer.Ordinal);
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _isDisposed;
        private string? _mimetype;

        public OdfPackageMode Mode => _mode;
        public string? MimeType => _mimetype;
        public IReadOnlyDictionary<string, string> Manifest => _manifest;
        internal IReadOnlyDictionary<string, OdfPackageEntry> Entries => _entries;
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
                // Sanitize and check Zip Slip
                string name = SanitizeEntryName(entry.FullName);

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

                // Add entry to our index
                var pkgEntry = new OdfPackageEntry(name, entry);
                _entries[name] = pkgEntry;
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
                    if (reader.LocalName == "file-entry" && reader.NamespaceURI == OdfNamespaces.Manifest)
                    {
                        string? path = reader.GetAttribute("full-path", OdfNamespaces.Manifest) ?? reader.GetAttribute("full-path");
                        string? mediaType = reader.GetAttribute("media-type", OdfNamespaces.Manifest) ?? reader.GetAttribute("media-type");

                        if (path != null && mediaType != null)
                        {
                            string normPath = path == "/" ? "/" : SanitizeEntryName(path);
                            _manifest[normPath] = mediaType;
                            
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
                        if (int.TryParse(keySizeStr, out int keySize))
                        {
                            currentEncryptionInfo.KeySize = keySize;
                        }
                        if (int.TryParse(iterationCountStr, out int iterationCount))
                        {
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
                        if (int.TryParse(startKeySizeStr, out int startKeySize))
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
                    SaveManifestToEntries();

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
                    SaveManifestToEntries();

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
                    SaveManifestToEntries();
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
                    SaveManifestToEntries();
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

                // Rest of manifest sorted by key for deterministic output (required for digital signatures)
                var sortedKeys = new List<string>(_manifest.Keys);
                sortedKeys.Sort(StringComparer.Ordinal);
                foreach (var key in sortedKeys)
                {
                    if (key == "/" || key == "mimetype" || key == "META-INF/manifest.xml") continue;

                    writer.WriteStartElement("file-entry", OdfNamespaces.Manifest);
                    writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, key);
                    writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, _manifest[key]);

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
                        writer.WriteAttributeString("manifest", "key-size", OdfNamespaces.Manifest, info.KeySize.ToString());
                        writer.WriteAttributeString("manifest", "iteration-count", OdfNamespaces.Manifest, info.IterationCount.ToString());
                        writer.WriteAttributeString("manifest", "salt", OdfNamespaces.Manifest, Convert.ToBase64String(info.Salt));
                        writer.WriteEndElement(); // key-derivation

                        if (!string.IsNullOrEmpty(info.StartKeyGenerationName) && info.StartKeySize.HasValue)
                        {
                            writer.WriteStartElement("start-key-generation", OdfNamespaces.Manifest);
                            writer.WriteAttributeString("manifest", "start-key-generation-name", OdfNamespaces.Manifest, info.StartKeyGenerationName);
                            writer.WriteAttributeString("manifest", "key-size", OdfNamespaces.Manifest, info.StartKeySize.Value.ToString());
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
            using var zip = new ZipArchive(targetStream, ZipArchiveMode.Create, true, Encoding.UTF8);

            // 1. mimetype MUST be first and Stored (uncompressed)
            if (_entries.TryGetValue("mimetype", out var mimeEntry))
            {
                var zipEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
                
                // Write fixed timestamp if Deterministic is enabled
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

    #endregion
}
