#pragma warning restore CS1591

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
using OdfKit.Compliance;
using OdfKit.DOM;
using OdfKit.Formula;
using OdfKit.Styles;

namespace OdfKit.Core;

/// <summary>
/// 表示 ODF 封裝的開啟模式。
/// </summary>
public enum OdfPackageMode
{
    /// <summary>
    /// 唯讀模式。
    /// </summary>
    Read,

    /// <summary>
    /// 讀寫模式。
    /// </summary>
    ReadWrite,

    /// <summary>
    /// 建立模式。
    /// </summary>
    Create
}

/// <summary>
/// 表示 ODF 文件的實體封裝。
/// </summary>
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

    /// <summary>
    /// 取得目前 ODF 封裝的開啟模式。
    /// </summary>
    public OdfPackageMode Mode => _mode;

    /// <summary>
    /// 取得或設定 ODF 封裝的 MIME 媒體類型。
    /// </summary>
    public string? MimeType => _mimetype;

    private OdfVersion _version = OdfVersionInfo.DefaultVersion;

    /// <summary>
    /// 取得或設定封裝文件的 ODF 規格版本。
    /// </summary>
    public OdfVersion Version
    {
        get => _version;
        set => _version = value;
    }

    /// <summary>
    /// 取得封裝的 RDF metadata 集合，對應 <c>META-INF/manifest.rdf</c>。
    /// </summary>
    public OdfRdfMetadata RdfMetadata { get; private set; } = new();

    /// <summary>
    /// 取得一個值，指出目前封裝是否為單一 Flat XML 檔案。
    /// </summary>
    public bool IsFlatXml
    {
        get => _isFlatXml;
        set => _isFlatXml = value;
    }

    /// <summary>
    /// 取得封裝內部所有項目的媒體類型資訊清單。
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
    /// 判斷指定路徑的項目是否已加密。
    /// </summary>
    /// <param name="name">項目的相對路徑名稱</param>
    /// <returns>若該項目已加密，則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    public bool IsEntryEncrypted(string name)
    {
        name = SanitizeEntryName(name);
        return _entries.TryGetValue(name, out var entry) && entry.EncryptionInfo != null;
    }

    /// <summary>
    /// 取得指定項目的加密詳細資訊。
    /// </summary>
    /// <param name="name">項目的相對路徑名稱</param>
    /// <returns>項目的加密資訊；若未加密則為 <see langword="null"/></returns>
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

}
