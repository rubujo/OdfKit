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

    /// <summary>
    /// 存檔掛鉤（字型內嵌）所使用的字型情境；由擁有此封裝的文件於存檔準備時指派，
    /// 獨立操作封裝時維持 <see cref="Styles.OdfFontContext.Default"/>。
    /// </summary>
    internal Styles.OdfFontContext FontContext { get; set; } = Styles.OdfFontContext.Default;

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
                    catch (Exception ex)
                    {
                        OdfKitDiagnostics.Warn("背景預讀失敗；後續主線程存取將重新載入並回報錯誤。", ex);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_prefetchCts.IsCancellationRequested)
        {
            OdfKitDiagnostics.Info("背景預讀處理器已依封裝處置要求停止。");
        }
    }
#endif


    #region Factory Methods


    /// <summary>
    /// Opens an existing ODF package from the specified file path.
    /// 從指定的檔案路徑開啟既有的 ODF 封裝。
    /// </summary>
    /// <returns>The opened <see cref="OdfPackage"/> instance. / 開啟的 <see cref="OdfPackage"/> 執行個體。</returns>
    public static OdfPackage Open(string path) => Open(path, null);

    /// <summary>
    /// Short overload of Open that accepts path and options; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 path 與 options；其餘可選參數使用預設值並轉呼叫最長 Open 多載。
    /// </summary>
    public static OdfPackage Open(string path, OdfLoadOptions? options)
    {
        OdfTransactionJournal.RecoverBeforeOpen(path);

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
    /// <remarks>
    /// When lazy loading is enabled, this method starts a background task that reads well-known entries (e.g. content.xml, styles.xml, meta.xml, settings.xml) from <paramref name="stream"/>; <see cref="Dispose()"/> and <see cref="DisposeAsync"/> both wait for this task to finish. Callers that pass <see langword="true"/> for  must fully dispose the returned <see cref="OdfPackage"/> (e.g. by scoping it in an explicit <c>using</c> block) before repositioning or reopening the same <paramref name="stream"/>; otherwise the background read and a subsequent foreground read race on the stream's cursor and can corrupt the read.
    /// 啟用延遲載入時，此方法會啟動一個背景工作，從 <paramref name="stream"/> 讀取已知專案（例如 content.xml、styles.xml、meta.xml、settings.xml）；<see cref="Dispose()"/> 與 <see cref="DisposeAsync"/> 都會等待此工作完成。呼叫端若對  傳入 <see langword="true"/>，必須在重新定位或重新開啟同一個 <paramref name="stream"/> 之前完整釋放傳回的 <see cref="OdfPackage"/>（例如以明確的 <c>using</c> 區塊限定其存活範圍），否則背景讀取與後續的前景讀取會競爭該資料流的游標，可能導致讀取內容毀損。
    /// </remarks>
    /// <returns>The opened <see cref="OdfPackage"/> instance. / 開啟的 <see cref="OdfPackage"/> 執行個體。</returns>
    public static OdfPackage Open(Stream stream) => Open(stream, false, null);

    /// <summary>
    /// Short overload of Open that accepts stream and leaveOpen; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream 與 leaveOpen；其餘可選參數使用預設值並轉呼叫最長 Open 多載。
    /// </summary>
    public static OdfPackage Open(Stream stream, bool leaveOpen) => Open(stream, leaveOpen, null);

    /// <summary>
    /// Short overload of Open that accepts stream and options; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream 與 options；其餘可選參數使用預設值並轉呼叫最長 Open 多載。
    /// </summary>
    public static OdfPackage Open(Stream stream, OdfLoadOptions? options) => Open(stream, false, options);

    /// <summary>
    /// Short overload of Open that accepts stream, leaveOpen, and options; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream、leaveOpen 與 options；其餘可選參數使用預設值並轉呼叫最長 Open 多載。
    /// </summary>
    public static OdfPackage Open(Stream stream, bool leaveOpen, OdfLoadOptions? options)
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
    /// <returns>A task representing the asynchronous open operation, whose result is the opened <see cref="OdfPackage"/> instance. / 代表非同步開啟作業的工作，其結果為開啟的 <see cref="OdfPackage"/> 執行個體。</returns>
    /// <remarks>
    /// 若  已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 ZIP 解壓與 manifest 載入期間協作檢查取消語彙。
    /// </remarks>
    public static Task<OdfPackage> OpenAsync(string path) => OpenAsync(path, null, default);

    /// <summary>
    /// Asynchronously opens an ODF package from a file path with a cancellation token.
    /// 以取消語彙基元非同步從檔案路徑開啟 ODF 封裝。
    /// </summary>
    /// <param name="path">The package file path. / 封裝檔案路徑。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task whose result is the opened package. / 代表非同步開啟作業的工作，其結果為已開啟的封裝。</returns>
    public static Task<OdfPackage> OpenAsync(string path, CancellationToken cancellationToken) =>
        OpenAsync(path, null, cancellationToken);

    /// <summary>
    /// Short overload of OpenAsync that accepts path and options; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 path 與 options；其餘可選參數使用預設值並轉呼叫最長 OpenAsync 多載。
    /// </summary>
    public static Task<OdfPackage> OpenAsync(string path, OdfLoadOptions? options) => OpenAsync(path, options, default);

    /// <summary>
    /// Short overload of OpenAsync that accepts path, options, and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 path、options 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 OpenAsync 多載。
    /// </summary>
    public static async Task<OdfPackage> OpenAsync(
        string path,
        OdfLoadOptions? options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OdfTransactionJournal.RecoverBeforeOpen(path);

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
    /// <returns>A task representing the asynchronous open operation, whose result is the opened <see cref="OdfPackage"/> instance. / 代表非同步開啟作業的工作，其結果為開啟的 <see cref="OdfPackage"/> 執行個體。</returns>
    /// <remarks>
    /// 若  已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 ZIP 解壓與 manifest 載入期間協作檢查取消語彙。
    /// </remarks>
    public static Task<OdfPackage> OpenAsync(Stream stream) => OpenAsync(stream, false, null, default);

    /// <summary>
    /// Asynchronously opens an ODF package from a stream with a cancellation token.
    /// 以取消語彙基元非同步從資料流開啟 ODF 封裝。
    /// </summary>
    /// <param name="stream">The package stream. / 封裝資料流。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task whose result is the opened package. / 代表非同步開啟作業的工作，其結果為已開啟的封裝。</returns>
    public static Task<OdfPackage> OpenAsync(Stream stream, CancellationToken cancellationToken) =>
        OpenAsync(stream, false, null, cancellationToken);

    /// <summary>
    /// Short overload of OpenAsync that accepts stream and leaveOpen; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream 與 leaveOpen；其餘可選參數使用預設值並轉呼叫最長 OpenAsync 多載。
    /// </summary>
    public static Task<OdfPackage> OpenAsync(Stream stream, bool leaveOpen) => OpenAsync(stream, leaveOpen, null, default);

    /// <summary>
    /// Asynchronously opens an ODF package from a stream with leave-open and cancellation options.
    /// 以是否保持資料流開啟與取消語彙基元非同步從資料流開啟 ODF 封裝。
    /// </summary>
    /// <param name="stream">The package stream. / 封裝資料流。</param>
    /// <param name="leaveOpen">Whether to leave the stream open. / 是否保持資料流開啟。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task whose result is the opened package. / 代表非同步開啟作業的工作，其結果為已開啟的封裝。</returns>
    public static Task<OdfPackage> OpenAsync(Stream stream, bool leaveOpen, CancellationToken cancellationToken) =>
        OpenAsync(stream, leaveOpen, null, cancellationToken);

    /// <summary>
    /// Short overload of OpenAsync that accepts stream and options; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream 與 options；其餘可選參數使用預設值並轉呼叫最長 OpenAsync 多載。
    /// </summary>
    public static Task<OdfPackage> OpenAsync(Stream stream, OdfLoadOptions? options) => OpenAsync(stream, false, options, default);

    /// <summary>
    /// Short overload of OpenAsync that accepts stream, leaveOpen, and options; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream、leaveOpen 與 options；其餘可選參數使用預設值並轉呼叫最長 OpenAsync 多載。
    /// </summary>
    public static Task<OdfPackage> OpenAsync(Stream stream, bool leaveOpen, OdfLoadOptions? options) => OpenAsync(stream, leaveOpen, options, default);

    /// <summary>
    /// Short overload of OpenAsync that accepts stream, leaveOpen, options, and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream、leaveOpen、options 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 OpenAsync 多載。
    /// </summary>
    public static async Task<OdfPackage> OpenAsync(
        Stream stream,
        bool leaveOpen,
        OdfLoadOptions? options,
        CancellationToken cancellationToken)
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
    /// <returns>The created <see cref="OdfPackage"/> instance. / 建立的 <see cref="OdfPackage"/> 執行個體。</returns>
    public static OdfPackage Create(string path) => Create(path, null);

    /// <summary>
    /// Short overload of Create that accepts path and options; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 path 與 options；其餘可選參數使用預設值並轉呼叫最長 Create 多載。
    /// </summary>
    public static OdfPackage Create(string path, OdfSaveOptions? options)
    {
        FileStream stream = new(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        return new OdfPackage(OdfPackageMode.Create, stream, false, null, options);
    }

    /// <summary>
    /// Creates a new ODF package in the specified stream.
    /// 在指定的資料流建立一個新的 ODF 封裝。
    /// </summary>
    /// <returns>The created <see cref="OdfPackage"/> instance. / 建立的 <see cref="OdfPackage"/> 執行個體。</returns>
    public static OdfPackage Create(Stream stream) => Create(stream, false, null);

    /// <summary>
    /// Short overload of Create that accepts stream and leaveOpen; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream 與 leaveOpen；其餘可選參數使用預設值並轉呼叫最長 Create 多載。
    /// </summary>
    public static OdfPackage Create(Stream stream, bool leaveOpen) => Create(stream, leaveOpen, null);

    /// <summary>
    /// Short overload of Create that accepts stream and options; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream 與 options；其餘可選參數使用預設值並轉呼叫最長 Create 多載。
    /// </summary>
    public static OdfPackage Create(Stream stream, OdfSaveOptions? options) => Create(stream, false, options);

    /// <summary>
    /// Short overload of Create that accepts stream, leaveOpen, and options; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream、leaveOpen 與 options；其餘可選參數使用預設值並轉呼叫最長 Create 多載。
    /// </summary>
    public static OdfPackage Create(Stream stream, bool leaveOpen, OdfSaveOptions? options)
    {
        return new OdfPackage(OdfPackageMode.Create, stream, leaveOpen, null, options);
    }


    #endregion

}
