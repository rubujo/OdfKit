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

            using var reader = XmlReader.Create(stream, settings);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && 
                    reader.LocalName == "file-entry" && 
                    reader.NamespaceURI == OdfNamespaces.Manifest)
                {
                    string? path = reader.GetAttribute("full-path", OdfNamespaces.Manifest);
                    string? mediaType = reader.GetAttribute("media-type", OdfNamespaces.Manifest);

                    if (path != null && mediaType != null)
                    {
                        // Standardize manifest path
                        string normPath = path == "/" ? "/" : SanitizeEntryName(path);
                        _manifest[normPath] = mediaType;
                    }
                }
            }
        }

        #endregion

        #region ZIP Path & Entry Sanitize (Zip Slip Protection)

        public static string SanitizeEntryName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            // Absolute path or UNC path check
            if (name.Contains(":") || name.StartsWith("\\\\") || name.StartsWith("//"))
            {
                throw new SecurityException($"Forbidden absolute path, drive specifier, or UNC format: {name}");
            }

            // Normalize backslashes to forward slashes
            string normalized = name.Replace('\\', '/');

            // Strip leading slashes
            while (normalized.StartsWith("/"))
            {
                normalized = normalized.Substring(1);
            }

            // Check for path traversal (Zip Slip)
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
                SaveManifestToEntries();
                WriteToArchive(destinationStream);
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
                SaveManifestToEntries();
                await Task.Run(() => WriteToArchive(destinationStream), cancellationToken).ConfigureAwait(false);
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
                    writer.WriteEndElement();
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
