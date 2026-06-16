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
            // 若已設定，於儲存時處理公式與字型嵌入
            ProcessSaveHooks();

            bool hasEncryption = _saveOptions.Password != null || _saveOptions.CryptographyProvider != null;
            if (hasEncryption)
            {
                OdfEncryption.Encrypt(this, _saveOptions.Password ?? string.Empty, _saveOptions.EncryptionAlgorithm);
            }
            try
            {
                // 序列化前先寫入或更新 manifest
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
            // 若已設定，於儲存時處理公式與字型嵌入
            ProcessSaveHooks();

            bool hasEncryption = _saveOptions.Password != null || _saveOptions.CryptographyProvider != null;
            if (hasEncryption)
            {
                OdfEncryption.Encrypt(this, _saveOptions.Password ?? string.Empty, _saveOptions.EncryptionAlgorithm);
            }
            try
            {
                // 序列化前先寫入或更新 manifest
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
        // 建置 manifest.xml
        using var ms = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false), // 不含 BOM 的 UTF-8
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

            // 根項目
            writer.WriteStartElement("file-entry", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, "/");
            writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, _mimetype ?? "application/vnd.oasis.opendocument.text");
            writer.WriteAttributeString("manifest", "version", OdfNamespaces.Manifest, versionText);
            writer.WriteEndElement();

            // 收集目錄項目
            var directories = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in _manifest.Keys)
            {
                int slashIdx = key.IndexOf('/');
                if (slashIdx != -1)
                {
                    string dir = key.Substring(0, slashIdx + 1);
                    if (!directories.ContainsKey(dir))
                    {
                        // 嘗試讀取此目錄的 mimetype
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

            // 其餘 manifest 項目依鍵值排序以確保確定性輸出（數位簽章所需）
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
                    writer.WriteEndElement(); // algorithm 元素

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
                        writer.WriteEndElement(); // encrypted-key 元素
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
                    writer.WriteEndElement(); // key-derivation 元素

                    if (!string.IsNullOrEmpty(info.StartKeyGenerationName) && info.StartKeySize.HasValue)
                    {
                        writer.WriteStartElement("start-key-generation", OdfNamespaces.Manifest);
                        writer.WriteAttributeString("manifest", "start-key-generation-name", OdfNamespaces.Manifest, info.StartKeyGenerationName);
                        writer.WriteAttributeString("manifest", "key-size", OdfNamespaces.Manifest, info.StartKeySize.Value.ToString(CultureInfo.InvariantCulture));
                        writer.WriteEndElement(); // start-key-generation 元素
                    }

                    writer.WriteEndElement(); // encryption-data 元素
                }

                writer.WriteEndElement(); // file-entry 元素
            }

            writer.WriteEndElement(); // manifest 元素
            writer.WriteEndDocument();
        }

        // WriteEntry 會處理簽章移除；內部產生 manifest 時暫時略過
        var manifestEntryName = "META-INF/manifest.xml";
        var pkgEntry = new OdfPackageEntry(manifestEntryName, ms.ToArray());
        _entries[manifestEntryName] = pkgEntry;
        _manifest[manifestEntryName] = "text/xml";
    }

    #endregion
}
