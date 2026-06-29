#pragma warning restore CS1591

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using OdfKit.Compliance;
using OdfKit.DOM;
using OdfKit.Formula;
using OdfKit.Spreadsheet;
using OdfKit.Styles;

namespace OdfKit.Core;

/// <summary>
/// The open mode of an ODF package.
/// 表示 ODF 封裝的開啟模式。
/// </summary>
public enum OdfPackageMode
{
    /// <summary>
    /// Read-only mode.
    /// 唯讀模式。
    /// </summary>
    Read,

    /// <summary>
    /// Read-write mode.
    /// 讀寫模式。
    /// </summary>
    ReadWrite,

    /// <summary>
    /// Create mode.
    /// 建立模式。
    /// </summary>
    Create
}

/// <summary>
/// Represents the physical package of an ODF document.
/// 表示 ODF 文件的實體封裝。
/// </summary>
[DebuggerTypeProxy(typeof(OdfPackageDebugView))]
public sealed partial class OdfPackage : IDisposable, IAsyncDisposable
{
    private const string RdfMetadataPath = "META-INF/manifest.rdf";
    private readonly OdfPackageMode _mode;
    private Stream? _underlyingStream;
    private readonly bool _leaveOpen;
    private readonly OdfLoadOptions _loadOptions;
    private OdfSaveOptions _saveOptions;

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

    internal string? FilePath { get; set; }
    internal System.IO.MemoryMappedFiles.MemoryMappedFile? Mmf { get; set; }
    internal Dictionary<string, OdfMmfEntryInfo>? MmfEntries { get; set; }
    internal System.Threading.Tasks.Task? PreloadTask { get; set; }
    internal event System.Action? OnRollback;
    internal OdfExternalLinkManager? FormulaExternalLinksForSave { get; set; }

#if NET10_0_OR_GREATER
    internal System.Threading.Channels.Channel<OdfPackageEntry>? _prefetchChannel;
    private System.Threading.Tasks.Task? _prefetchProcessorTask;
    private readonly System.Threading.CancellationTokenSource _prefetchCts = new();
#endif

    /// <summary>
    /// Gets the open mode of the current ODF package.
    /// 取得目前 ODF 封裝的開啟模式。
    /// </summary>
    public OdfPackageMode Mode => _mode;

    /// <summary>
    /// Gets or sets the MIME media type of the ODF package.
    /// 取得或設定 ODF 封裝的 MIME 媒體類型。
    /// </summary>
    public string? MimeType => _mimetype;

    private OdfVersion _version = OdfVersionInfo.DefaultVersion;

    /// <summary>
    /// Gets or sets the ODF specification version of the packaged document.
    /// 取得或設定封裝文件的 ODF 規格版本。
    /// </summary>
    public OdfVersion Version
    {
        get => _version;
        set => _version = value;
    }

    /// <summary>
    /// Gets the package's RDF metadata collection, corresponding to <c>META-INF/manifest.rdf</c>.
    /// 取得封裝的 RDF metadata 集合，對應 <c>META-INF/manifest.rdf</c>。
    /// </summary>
    public OdfRdfMetadata RdfMetadata { get; private set; } = new();

    internal void SetRdfMetadata(OdfRdfMetadata metadata) => RdfMetadata = metadata;

    private OdfMediaManager? _mediaManager;

    /// <summary>
    /// Gets the media manager instance for this package.
    /// 取得此封裝套件的媒體管理器實例。
    /// </summary>
    public OdfMediaManager MediaManager => _mediaManager ??= new OdfMediaManager(this);

    /// <summary>
    /// Gets a value indicating whether the current package is a single flat XML file.
    /// 取得一個值，指出目前封裝是否為單一 Flat XML 檔案。
    /// </summary>
    public bool IsFlatXml
    {
        get => _isFlatXml;
        set => _isFlatXml = value;
    }

    /// <summary>
    /// Gets the media type information list for all entries in the package.
    /// 取得封裝內部所有專案的媒體類型資訊清單。
    /// </summary>
    public IReadOnlyDictionary<string, string> Manifest => _manifest;
    internal IReadOnlyDictionary<string, OdfPackageEntry> Entries => _entries;
    internal IReadOnlyList<string> EntryOrder => _entryOrder;
    internal IReadOnlyList<string> DuplicateEntryNames => _duplicateEntryNames;
    internal IReadOnlyList<string> DuplicateManifestPaths => _duplicateManifestPaths;
    internal IReadOnlyList<OdfManifestFileEntryIssue> ManifestFileEntryIssues => _manifestFileEntryIssues;
    internal OdfManifestRootInfo? ManifestRootInfo => _manifestRootInfo;
    internal OdfLoadOptions LoadOptions => _loadOptions;
    internal OdfSaveOptions SaveOptions => _saveOptions;

    /// <summary>
    /// Determines whether the entry at the specified path is encrypted.
    /// 判斷指定路徑的專案是否已加密。
    /// </summary>
    /// <param name="name">The relative path name of the entry. / 專案的相對路徑名稱。</param>
    /// <returns><see langword="true"/> if the entry is encrypted; otherwise <see langword="false"/>. / 若該專案已加密，則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool IsEntryEncrypted(string name)
        => OdfPackageEntryAccessEngine.IsEntryEncrypted(EntryCollaborators, name);

    /// <summary>
    /// Finds the encryption details of the specified entry.
    /// 尋找指定專案的加密詳細資訊。
    /// </summary>
    /// <param name="name">The relative path name of the entry. / 專案的相對路徑名稱。</param>
    /// <returns>The entry's encryption information, or <see langword="null"/> if not encrypted. / 專案的加密資訊；若未加密則為 <see langword="null"/>。</returns>
    public OdfEncryptionInfo? FindEntryEncryptionInfo(string name)
        => OdfPackageEntryAccessEngine.FindEntryEncryptionInfo(EntryCollaborators, name);

    private OdfPackage(OdfPackageMode mode, Stream? underlyingStream, bool leaveOpen, OdfLoadOptions? loadOptions, OdfSaveOptions? saveOptions)
    {
        _mode = mode;
        _underlyingStream = underlyingStream;
        _leaveOpen = leaveOpen;
        _loadOptions = loadOptions ?? OdfLoadOptions.Default;
        _saveOptions = saveOptions ?? OdfSaveOptions.Default;

#if NET10_0_OR_GREATER
        if (_mode == OdfPackageMode.ReadWrite || _mode == OdfPackageMode.Read)
        {
            _prefetchChannel = System.Threading.Channels.Channel.CreateUnbounded<OdfPackageEntry>(new System.Threading.Channels.UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
            _prefetchProcessorTask = Task.Run(ProcessPrefetchQueueAsync);
        }
#endif
    }

#if NET10_0_OR_GREATER
    private async Task ProcessPrefetchQueueAsync()
    {
        if (_prefetchChannel == null)
            return;
        var reader = _prefetchChannel.Reader;
        try
        {
            while (await reader.WaitToReadAsync(_prefetchCts.Token).ConfigureAwait(false))
            {
                while (reader.TryRead(out var entry))
                {
                    try
                    {
                        entry.EnsureBytesLoaded();
                    }
                    catch
                    {
                        // 忽略預讀異常，待主線程存取時處理
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }
#endif


    #region Factory Methods


    /// <summary>
    /// Opens an existing ODF package from the specified file path.
    /// 從指定的檔案路徑開啟既有的 ODF 封裝。
    /// </summary>
    /// <param name="path">The path of the ODF file. / ODF 檔案的路徑。</param>
    /// <param name="options">The load options. / 載入選項。</param>
    /// <returns>The opened <see cref="OdfPackage"/> instance. / 開啟的 <see cref="OdfPackage"/> 執行個體。</returns>
    public static OdfPackage Open(string path, OdfLoadOptions? options = null)
    {
        string journalPath = path + ".journal";
        if (File.Exists(journalPath))
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Copy(journalPath, path, true);
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    fs.Flush(true);
                }
                File.Delete(journalPath);
            }
            catch (Exception ex)
            {
                throw new IOException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_OdfPackage_JournalCreateFailed"), ex);
            }
        }

        Stream stream = options?.EnableDirectIo == true
            ? new OdfDirectIoReadableStream(path)
            : new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        OdfPackage package = new(OdfPackageMode.ReadWrite, stream, false, options, null);
        package.FilePath = path;
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

    /// <summary>
    /// Opens an existing ODF package from the specified stream.
    /// 從指定的資料流開啟既有的 ODF 封裝。
    /// </summary>
    /// <param name="stream">The stream containing the ODF package data. / 包含 ODF 封裝資料的資料流。</param>
    /// <param name="leaveOpen"><see langword="true"/> to keep the stream open after the package is disposed; otherwise <see langword="false"/>. / 若在處置封裝後保持資料流開啟，則為 <see langword="true"/>；否則為 <see langword="false"/>。</param>
    /// <param name="options">The load options. / 載入選項。</param>
    /// <returns>The opened <see cref="OdfPackage"/> instance. / 開啟的 <see cref="OdfPackage"/> 執行個體。</returns>
    public static OdfPackage Open(Stream stream, bool leaveOpen = false, OdfLoadOptions? options = null)
    {
        OdfPackage package = new(OdfPackageMode.ReadWrite, stream, leaveOpen, options, null);
        if (stream is FileStream fs)
        {
            package.FilePath = fs.Name;
        }
        else if (stream is OdfDirectIoReadableStream ds)
        {
            package.FilePath = ds.FilePath;
        }
        try
        {
            package.InitializeLoad();
            return package;
        }
        catch
        {
            if (!leaveOpen)
                stream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Asynchronously opens an existing ODF package from the specified file path.
    /// 非同步從指定的檔案路徑開啟既有的 ODF 封裝。
    /// </summary>
    /// <param name="path">The path of the ODF file. / ODF 檔案的路徑。</param>
    /// <param name="options">The load options. / 載入選項。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous open operation, whose result is the opened <see cref="OdfPackage"/> instance. / 代表非同步開啟作業的工作，其結果為開啟的 <see cref="OdfPackage"/> 執行個體。</returns>
    /// <remarks>
    /// 若 <paramref name="cancellationToken"/> 已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 ZIP 解壓與 manifest 載入期間協作檢查取消語彙。
    /// </remarks>
    public static async Task<OdfPackage> OpenAsync(
        string path,
        OdfLoadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        string journalPath = path + ".journal";
        if (File.Exists(journalPath))
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Copy(journalPath, path, true);
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    fs.Flush(true);
                }
                File.Delete(journalPath);
            }
            catch (Exception ex)
            {
                throw new IOException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_OdfPackage_JournalCreateFailed"), ex);
            }
        }

        Stream stream = options?.EnableDirectIo == true
            ? new OdfDirectIoReadableStream(path)
            : new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        OdfPackage package = new(OdfPackageMode.ReadWrite, stream, false, options, null);
        package.FilePath = path;
        try
        {
            await package.InitializeLoadAsync(cancellationToken).ConfigureAwait(false);
            return package;
        }
        catch
        {
            await package.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Asynchronously opens an existing ODF package from the specified stream.
    /// 非同步從指定的資料流開啟既有的 ODF 封裝。
    /// </summary>
    /// <param name="stream">The stream containing the ODF package data. / 包含 ODF 封裝資料的資料流。</param>
    /// <param name="leaveOpen"><see langword="true"/> to keep the stream open after the package is disposed; otherwise <see langword="false"/>. / 若在處置封裝後保持資料流開啟，則為 <see langword="true"/>；否則為 <see langword="false"/>。</param>
    /// <param name="options">The load options. / 載入選項。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous open operation, whose result is the opened <see cref="OdfPackage"/> instance. / 代表非同步開啟作業的工作，其結果為開啟的 <see cref="OdfPackage"/> 執行個體。</returns>
    /// <remarks>
    /// 若 <paramref name="cancellationToken"/> 已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 ZIP 解壓與 manifest 載入期間協作檢查取消語彙。
    /// </remarks>
    public static async Task<OdfPackage> OpenAsync(
        Stream stream,
        bool leaveOpen = false,
        OdfLoadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        OdfPackage package = new(OdfPackageMode.ReadWrite, stream, leaveOpen, options, null);
        if (stream is FileStream fs)
        {
            package.FilePath = fs.Name;
        }
        else if (stream is OdfDirectIoReadableStream ds)
        {
            package.FilePath = ds.FilePath;
        }
        try
        {
            await package.InitializeLoadAsync(cancellationToken).ConfigureAwait(false);
            return package;
        }
        catch
        {
            if (!leaveOpen)
            {
                if (stream is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                else
                    stream.Dispose();
            }

            throw;
        }
    }

    /// <summary>
    /// Creates a new ODF package at the specified file path.
    /// 在指定的檔案路徑建立一個新的 ODF 封裝。
    /// </summary>
    /// <param name="path">The file path at which to create the package. / 要建立的檔案路徑。</param>
    /// <param name="options">The save and encryption options. / 儲存與加密選項。</param>
    /// <returns>The created <see cref="OdfPackage"/> instance. / 建立的 <see cref="OdfPackage"/> 執行個體。</returns>
    public static OdfPackage Create(string path, OdfSaveOptions? options = null)
    {
        FileStream stream = new(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        return new OdfPackage(OdfPackageMode.Create, stream, false, null, options);
    }

    /// <summary>
    /// Creates a new ODF package in the specified stream.
    /// 在指定的資料流建立一個新的 ODF 封裝。
    /// </summary>
    /// <param name="stream">The stream to which the ODF package is written. / 要寫入 ODF 封裝的資料流。</param>
    /// <param name="leaveOpen"><see langword="true"/> to keep the stream open after the package is disposed; otherwise <see langword="false"/>. / 若在處置封裝後保持資料流開啟，則為 <see langword="true"/>；否則為 <see langword="false"/>。</param>
    /// <param name="options">The save and encryption options. / 儲存與加密選項。</param>
    /// <returns>The created <see cref="OdfPackage"/> instance. / 建立的 <see cref="OdfPackage"/> 執行個體。</returns>
    public static OdfPackage Create(Stream stream, bool leaveOpen = false, OdfSaveOptions? options = null)
    {
        return new OdfPackage(OdfPackageMode.Create, stream, leaveOpen, null, options);
    }


    #endregion

}
