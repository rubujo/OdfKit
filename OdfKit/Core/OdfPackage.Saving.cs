using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula;
using OdfKit.Styles;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    #region Saving and Atomic Save


    /// <summary>
    /// 將所有變更儲存回原來的檔案或資料流中。
    /// </summary>
    /// <param name="options">單次儲存設定選項；若為 <see langword="null"/>，則使用封裝預設選項</param>
    public void Save(OdfSaveOptions? options = null)
    {
        if (_mode == OdfPackageMode.Read)
        {
            throw new InvalidOperationException("Cannot save a read-only ODF package.");
        }

        _lock.Wait();
        OdfSaveOptions previousOptions = UseSaveOptions(options);
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
                    SaveRdfMetadataToEntries();
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
            _saveOptions = previousOptions;
            _lock.Release();
        }
    }

    /// <summary>
    /// 將所有變更儲存回原來的檔案或資料流中（非同步）。
    /// </summary>
    /// <param name="cancellationToken">取消語彙</param>
    /// <returns>代表非同步作業的工作</returns>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await SaveAsync(null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 使用指定儲存選項，將所有變更儲存回原來的檔案或資料流中（非同步）。
    /// </summary>
    /// <param name="options">單次儲存設定選項；若為 <see langword="null"/>，則使用封裝預設選項</param>
    /// <param name="cancellationToken">取消語彙</param>
    /// <returns>代表非同步作業的工作</returns>
    public async Task SaveAsync(OdfSaveOptions? options, CancellationToken cancellationToken = default)
    {
        if (_mode == OdfPackageMode.Read)
        {
            throw new InvalidOperationException("Cannot save a read-only ODF package.");
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        OdfSaveOptions previousOptions = UseSaveOptions(options);
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
                    SaveRdfMetadataToEntries();
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
            _saveOptions = previousOptions;
            _lock.Release();
        }
    }

    /// <summary>
    /// 將封裝序列化儲存至指定的目的地資料流。
    /// </summary>
    /// <param name="destinationStream">目標目的地資料流</param>
    /// <param name="options">單次儲存設定選項；若為 <see langword="null"/>，則使用封裝預設選項</param>
    public void SaveToStream(Stream destinationStream, OdfSaveOptions? options = null)
    {
        if (destinationStream == null)
            throw new ArgumentNullException(nameof(destinationStream));

        _lock.Wait();
        OdfSaveOptions previousOptions = UseSaveOptions(options);
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
            _saveOptions = previousOptions;
            _lock.Release();
        }
    }

    /// <summary>
    /// 將封裝序列化儲存至指定的目的地資料流（非同步）。
    /// </summary>
    /// <param name="destinationStream">目標目的地資料流</param>
    /// <param name="cancellationToken">取消語彙</param>
    /// <returns>代表非同步作業的工作</returns>
    public async Task SaveToStreamAsync(Stream destinationStream, CancellationToken cancellationToken = default)
    {
        await SaveToStreamAsync(destinationStream, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 使用指定儲存選項，將封裝序列化儲存至指定的目的地資料流（非同步）。
    /// </summary>
    /// <param name="destinationStream">目標目的地資料流</param>
    /// <param name="options">單次儲存設定選項；若為 <see langword="null"/>，則使用封裝預設選項</param>
    /// <param name="cancellationToken">取消語彙</param>
    /// <returns>代表非同步作業的工作</returns>
    public async Task SaveToStreamAsync(Stream destinationStream, OdfSaveOptions? options, CancellationToken cancellationToken = default)
    {
        if (destinationStream == null)
            throw new ArgumentNullException(nameof(destinationStream));

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        OdfSaveOptions previousOptions = UseSaveOptions(options);
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
            _saveOptions = previousOptions;
            _lock.Release();
        }
    }

    private OdfSaveOptions UseSaveOptions(OdfSaveOptions? options)
    {
        OdfSaveOptions previousOptions = _saveOptions;
        if (options is not null)
        {
            _saveOptions = options;
        }

        return previousOptions;
    }

    private void LoadRdfMetadata()
    {
        RdfMetadata = new OdfRdfMetadata();
        if (!_entries.TryGetValue(RdfMetadataPath, out var entry))
        {
            return;
        }

        try
        {
            using var stream = entry.OpenReader();
            RdfMetadata = OdfRdfParser.Parse(stream, _loadOptions.MaxXmlCharactersInDocument);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException("ODF RDF metadata 不是有效的 RDF/XML。", ex);
        }
    }

    private void SaveRdfMetadataToEntries()
    {
        if (!RdfMetadata.IsDirty || RdfMetadata.Triples.Count == 0)
        {
            return;
        }

        byte[] content = OdfRdfParser.Serialize(RdfMetadata, _saveOptions.IndentXml);
        WriteEntry(RdfMetadataPath, content, "application/rdf+xml");
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
            string versionText = Version switch
            {
                OdfVersion.Odf10 => "1.0",
                OdfVersion.Odf11 => "1.1",
                OdfVersion.Odf12 => "1.2",
                OdfVersion.Odf13 => "1.3",
                OdfVersion.Odf14 => "1.4",
                _ => "1.4"
            };
            writer.WriteStartElement("manifest", "manifest", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "version", OdfNamespaces.Manifest, versionText);

            // Root entry
            writer.WriteStartElement("file-entry", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, "/");
            writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, _mimetype ?? "application/vnd.oasis.opendocument.text");
            writer.WriteAttributeString("manifest", "version", OdfNamespaces.Manifest, versionText);
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
                            catch { }
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
                if (key == "/" || key == "mimetype" || key == "META-INF/manifest.xml")
                    continue;

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

                    foreach (var encryptedKey in info.OpenPgpEncryptedKeys)
                    {
                        writer.WriteStartElement("encrypted-key", OdfNamespaces.Manifest);
                        if (!string.IsNullOrEmpty(encryptedKey.KeyId))
                        {
                            writer.WriteAttributeString("manifest", "key-id", OdfNamespaces.Manifest, encryptedKey.KeyId);
                        }
                        if (!string.IsNullOrEmpty(encryptedKey.Recipient))
                        {
                            writer.WriteAttributeString("manifest", "recipient", OdfNamespaces.Manifest, encryptedKey.Recipient);
                        }
                        if (!string.IsNullOrEmpty(encryptedKey.AlgorithmName))
                        {
                            writer.WriteAttributeString("manifest", "algorithm-name", OdfNamespaces.Manifest, encryptedKey.AlgorithmName);
                        }
                        foreach (var prop in encryptedKey.ExtensionProperties)
                        {
                            writer.WriteAttributeString("manifest", prop.Key, OdfNamespaces.Manifest, prop.Value);
                        }
                        if (encryptedKey.KeyPacket.Length > 0)
                        {
                            writer.WriteString(Convert.ToBase64String(encryptedKey.KeyPacket));
                        }
                        writer.WriteEndElement(); // encrypted-key
                    }

                    writer.WriteStartElement("key-derivation", OdfNamespaces.Manifest);
                    writer.WriteAttributeString("manifest", "key-derivation-name", OdfNamespaces.Manifest, info.KeyDerivationName);
                    writer.WriteAttributeString("manifest", "key-size", OdfNamespaces.Manifest, info.KeySize.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("manifest", "iteration-count", OdfNamespaces.Manifest, info.IterationCount.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("manifest", "salt", OdfNamespaces.Manifest, Convert.ToBase64String(info.Salt));

                    if (info.ExtensionProperties != null)
                    {
                        foreach (var prop in info.ExtensionProperties)
                        {
                            if (prop.Key == "kdf-name")
                            {
                                writer.WriteAttributeString("loext", "kdf-name", "urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0", prop.Value);
                            }
                            else if (prop.Key == "argon2-t")
                            {
                                writer.WriteAttributeString("loext", "argon2-t", "urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0", prop.Value);
                            }
                            else if (prop.Key == "argon2-m")
                            {
                                writer.WriteAttributeString("loext", "argon2-m", "urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0", prop.Value);
                            }
                            else if (prop.Key == "argon2-p")
                            {
                                writer.WriteAttributeString("loext", "argon2-p", "urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0", prop.Value);
                            }
                        }
                    }
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
                if (contentRoot != null)
                    contentModified = true;
                if (stylesRoot != null)
                    stylesModified = true;
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

        // 2. Write all other entries
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
}
