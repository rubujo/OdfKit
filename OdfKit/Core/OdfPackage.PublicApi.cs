using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OdfKit.Compliance;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    #region Public API

    /// <summary>
    /// 檢查封裝中是否包含指定名稱的專案。
    /// </summary>
    /// <param name="name">專案的相對路徑名稱</param>
    /// <returns>若專案存在則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    public bool HasEntry(string name)
        => OdfPackageEntryAccessEngine.HasEntry(EntryCollaborators, name);

    /// <summary>
    /// 提供 ODF 封裝中實體專案的基本資訊。
    /// </summary>
    public class OdfPackageEntryInfo
    {
        /// <summary>
        /// 取得專案的相對路徑。
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// 初始化 <see cref="OdfPackageEntryInfo"/> 類別的新執行個體。
        /// </summary>
        /// <param name="path">專案的相對路徑</param>
        public OdfPackageEntryInfo(string path) => Path = path;
    }

    /// <summary>
    /// 取得封裝中所有實體專案的資訊集合。
    /// </summary>
    /// <returns>所有專案的資訊集合</returns>
    public IEnumerable<OdfPackageEntryInfo> GetEntries()
        => OdfPackageEntryAccessEngine.GetEntries(EntryCollaborators);

    /// <summary>
    /// 讀取指定路徑專案的完整內容位元組。
    /// </summary>
    /// <param name="path">專案的相對路徑名稱</param>
    /// <returns>專案的位元組陣列內容</returns>
    public byte[] ReadEntry(string path)
        => OdfPackageEntryAccessEngine.ReadEntry(EntryCollaborators, path);

    /// <summary>
    /// 將目前 ODF 封裝儲存到指定的目標資料流中。
    /// </summary>
    /// <param name="stream">要寫入的目標資料流</param>
    /// <param name="options">單次儲存設定選項；若為 <see langword="null"/>，則使用封裝預設選項</param>
    public void Save(Stream stream, OdfSaveOptions? options = null)
    {
        SaveToStream(stream, options);
    }

    /// <summary>
    /// 取得指定專案的唯讀資料流。
    /// </summary>
    /// <param name="name">專案的相對路徑名稱</param>
    /// <returns>代表專案內容的資料流</returns>
    public Stream GetEntryStream(string name)
        => OdfPackageEntryAccessEngine.GetEntryStream(EntryCollaborators, name);

    internal OdfPackageEntry? GetEntry(string name)
    {
        name = SanitizeEntryName(name);
        return _entries.TryGetValue(name, out var entry) ? entry : null;
    }

    /// <summary>
    /// 將指定的位元組內容寫入或覆寫封裝中的專案。
    /// </summary>
    /// <param name="name">專案的相對路徑名稱</param>
    /// <param name="content">要寫入的位元組內容</param>
    /// <param name="mediaType">專案的 MIME 媒體類型；未指定時自動依路徑判定</param>
    public void WriteEntry(string name, byte[] content, string? mediaType = null)
        => OdfPackageEntryAccessEngine.WriteEntry(EntryCollaborators, name, content, mediaType);

    /// <summary>
    /// 將指定的資料流內容寫入或覆寫封裝中的專案。
    /// </summary>
    /// <param name="name">專案的相對路徑名稱</param>
    /// <param name="contentStream">要寫入的內容來源資料流</param>
    /// <param name="mediaType">專案的 MIME 媒體類型；未指定時自動依路徑判定</param>
    public void WriteEntry(string name, Stream contentStream, string? mediaType = null)
        => OdfPackageEntryAccessEngine.WriteEntry(EntryCollaborators, name, contentStream, mediaType);

    /// <summary>
    /// 將指定的位元組內容新增至封裝；若同名專案已存在，則覆寫該專案。
    /// </summary>
    /// <param name="name">專案的相對路徑名稱</param>
    /// <param name="content">要新增的位元組內容</param>
    /// <param name="mediaType">專案的 MIME 媒體類型；未指定時自動依路徑判定</param>
    public void AddEntry(string name, byte[] content, string? mediaType = null)
        => WriteEntry(name, content, mediaType);

    /// <summary>
    /// 將指定的資料流內容新增至封裝；若同名專案已存在，則覆寫該專案。
    /// </summary>
    /// <param name="name">專案的相對路徑名稱</param>
    /// <param name="contentStream">要新增的內容來源資料流</param>
    /// <param name="mediaType">專案的 MIME 媒體類型；未指定時自動依路徑判定</param>
    public void AddEntry(string name, Stream contentStream, string? mediaType = null)
        => WriteEntry(name, contentStream, mediaType);

    /// <summary>
    /// 從封裝中移除指定的專案。
    /// </summary>
    /// <param name="name">要移除的專案相對路徑名稱</param>
    public void RemoveEntry(string name)
        => OdfPackageEntryAccessEngine.RemoveEntry(EntryCollaborators, name);

    /// <summary>
    /// 清理封裝中未被參照的圖片等媒體檔案。
    /// </summary>
    /// <param name="referencedMediaPaths">所有目前正被參照的媒體檔案路徑集合</param>
    /// <remarks>
    /// 此方法僅依路徑清單比對移除 <c>Pictures/</c> 下的 ZIP 媒體專案，不會檢查或同步移除
    /// <c>content.xml</c>／<c>styles.xml</c> 中殘留的 <c>draw:image</c> DOM 參照節點。
    /// 呼叫端必須自行確保 <paramref name="referencedMediaPaths"/> 與目前 DOM 實際參照狀態一致，
    /// 否則殘留的 DOM 參照會指向已被刪除的媒體專案而形成懸空連結，可能導致真實 ODF 應用程式
    /// （例如 LibreOffice）拒絕開啟整份文件。
    /// </remarks>
    public void PruneUnusedMedia(IEnumerable<string> referencedMediaPaths)
        => OdfPackageEntryAccessEngine.PruneUnusedMedia(EntryCollaborators, referencedMediaPaths);

    /// <summary>
    /// 設定 ODF 封裝的主要 MIME 媒體類型。
    /// </summary>
    /// <param name="mimetype">媒體類型字串</param>
    public void SetMimeType(string mimetype)
        => OdfPackageEntryAccessEngine.SetMimeType(EntryCollaborators, mimetype);

    #endregion

    #region Embedded Objects Extraction

    /// <summary>
    /// 取得此封裝中所內嵌的 ODF 物件資料夾路徑清單。
    /// </summary>
    /// <returns>內嵌物件路徑的集合</returns>
    public IEnumerable<string> GetEmbeddedObjects()
        => OdfPackageEntryAccessEngine.GetEmbeddedObjects(EntryCollaborators);

    /// <summary>
    /// 擷取內嵌物件的主要內容 XML 資料流。
    /// </summary>
    /// <param name="objectName">內嵌物件的路徑名稱</param>
    /// <returns>內嵌物件內容的資料流</returns>
    public Stream ExtractObjectStream(string objectName)
        => OdfPackageEntryAccessEngine.ExtractObjectStream(EntryCollaborators, objectName);

    /// <summary>
    /// 支援免 DOM 解析的原始二進位直改。
    /// </summary>
    /// <param name="entryName">專案的相對路徑名稱</param>
    /// <param name="patcher">直改委派，傳入原始內容 ReadOnlySpan，寫入目標 IBufferWriter，回傳是否發生變更</param>
    /// <returns>是否確實發生變更</returns>
    public bool RawEntryPatch(string entryName, OdfRawEntryPatcher patcher)
    {
        if (entryName == null)
            throw new ArgumentNullException(nameof(entryName));
        if (patcher == null)
            throw new ArgumentNullException(nameof(patcher));

        entryName = SanitizeEntryName(entryName);
        if (!_entries.TryGetValue(entryName, out var entry))
        {
            throw new FileNotFoundException(OdfLocalizer.GetMessage("Err_OdfPackageEntryAccessEngine_EntryNotFound", entryName));
        }

        byte[] originalBytes;
        using (var reader = entry.OpenReader())
        {
            if (reader is MemoryStream ms)
            {
                originalBytes = ms.ToArray();
            }
            else
            {
                using var temp = new MemoryStream();
                reader.CopyTo(temp);
                originalBytes = temp.ToArray();
            }
        }

        var writer = new SimpleBufferWriter(originalBytes.Length);
        bool isModified = patcher(originalBytes, writer);
        if (isModified)
        {
            byte[] newBytes = writer.WrittenReadOnlySpan.ToArray();
            entry.SetContent(newBytes);

            // MMF 原位二進位 Patch 與 CRC32 覆寫
            if (Mmf != null && MmfEntries != null && MmfEntries.TryGetValue(entryName, out var mmfEntry) && mmfEntry.CompressionMethod == 0)
            {
                if (newBytes.Length == mmfEntry.UncompressedSize)
                {
                    try
                    {
                        unsafe
                        {
                            using var accessor = Mmf.CreateViewAccessor(mmfEntry.CompressedDataOffset, mmfEntry.UncompressedSize, System.IO.MemoryMappedFiles.MemoryMappedFileAccess.Write);
                            byte* ptr = null;
                            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                            try
                            {
                                var destSpan = new Span<byte>(ptr + accessor.PointerOffset, (int)mmfEntry.UncompressedSize);
                                newBytes.CopyTo(destSpan);
                            }
                            finally
                            {
                                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                            }
                        }

                        // 重新計算 CRC32 並寫回 Local File Header
                        uint newCrc = OdfCrc32.Compute(newBytes);
                        unsafe
                        {
                            using var accessor = Mmf.CreateViewAccessor(mmfEntry.LocalHeaderOffset + 14, 4, System.IO.MemoryMappedFiles.MemoryMappedFileAccess.Write);
                            byte* ptr = null;
                            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                            try
                            {
                                *(uint*)(ptr + accessor.PointerOffset) = newCrc;
                            }
                            finally
                            {
                                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        OdfKitDiagnostics.Warn($"[OdfPackage] MMF 原位二進位 Patch / CRC32 覆寫失敗: {ex.Message}");
                    }
                }
            }

            if (!_manifest.ContainsKey(entryName))
            {
                string resolvedMediaType = OdfPackageMediaTypeResolver.Resolve(entryName, null);
                _manifest[entryName] = resolvedMediaType;
            }

            if (entryName != "META-INF/documentsignatures.xml" && entryName != "META-INF/manifest.xml")
            {
                RemoveOutdatedSignatures();
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// 傳回代表封裝內部虛擬檔案系統（VFS）結構的視覺化佈局字串。
    /// </summary>
    /// <returns>VFS 結構的視覺化字串</returns>
    public string DumpVfsLayout()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[VFS Root] MimeType: {MimeType ?? "N/A"}, Version: {Version}");

        foreach (OdfPackageDebugEntry entry in OdfPackageDebugEntry.CreateEntries(this))
        {
            string details = $"[Size: {entry.Size} bytes, Compressed: {entry.Compressed}, Dirty: {entry.Dirty}, LocalHeaderOffset: {entry.LocalHeaderOffset}, DataOffset: {entry.CompressedDataOffset}, MediaType: {entry.MediaType}{(entry.Encrypted ? ", Encrypted" : string.Empty)}]";
            sb.AppendLine($"  ├── {entry.Path} {details}");
        }

        return sb.ToString();
    }

    #endregion

    private sealed class SimpleBufferWriter : System.Buffers.IBufferWriter<byte>
    {
        private byte[] _buffer;
        private int _written;

        public SimpleBufferWriter(int initialCapacity = 256)
        {
            _buffer = new byte[initialCapacity];
        }

        public ReadOnlySpan<byte> WrittenReadOnlySpan => _buffer.AsSpan(0, _written);

        public void Advance(int count)
        {
            if (count < 0)
                throw new ArgumentException(nameof(count));
            if (_written + count > _buffer.Length)
                throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfPackage_CannotAdvancePastBufferedLength"));
            _written += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsMemory(_written);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsSpan(_written);
        }

        private void EnsureCapacity(int sizeHint)
        {
            if (sizeHint == 0)
                sizeHint = 1;
            int needed = _written + sizeHint;
            if (needed > _buffer.Length)
            {
                int newLen = Math.Max(_buffer.Length * 2, needed);
                byte[] newBuf = new byte[newLen];
                System.Buffer.BlockCopy(_buffer, 0, newBuf, 0, _written);
                _buffer = newBuf;
            }
        }
    }
}

internal sealed class OdfPackageDebugView(OdfPackage package)
{
    private readonly OdfPackage _package = package ?? throw new ArgumentNullException(nameof(package));

    public string MimeType => _package.MimeType ?? "unknown";
    public OdfVersion Version => _package.Version;

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public OdfPackageDebugEntry[] Entries => OdfPackageDebugEntry.CreateEntries(_package);
}

internal sealed class OdfPackageDebugEntry
{
    private OdfPackageDebugEntry(
        string path,
        string mediaType,
        long size,
        bool compressed,
        bool encrypted,
        bool dirty,
        long localHeaderOffset,
        long compressedDataOffset,
        long compressedSize,
        ushort? compressionMethod)
    {
        Path = path;
        MediaType = mediaType;
        Size = size;
        Compressed = compressed;
        Encrypted = encrypted;
        Dirty = dirty;
        LocalHeaderOffset = localHeaderOffset;
        CompressedDataOffset = compressedDataOffset;
        CompressedSize = compressedSize;
        CompressionMethod = compressionMethod;
    }

    public string Path { get; }

    public string MediaType { get; }

    public long Size { get; }

    public bool Compressed { get; }

    public bool Encrypted { get; }

    public bool Dirty { get; }

    public long LocalHeaderOffset { get; }

    public long CompressedDataOffset { get; }

    public long CompressedSize { get; }

    public ushort? CompressionMethod { get; }

    public override string ToString()
        => $"{Path} ({MediaType}, {Size} bytes, Dirty: {Dirty})";

    internal static OdfPackageDebugEntry[] CreateEntries(OdfPackage package)
    {
        return package.Manifest
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => CreateEntry(package, kv.Key, kv.Value))
            .ToArray();
    }

    private static OdfPackageDebugEntry CreateEntry(OdfPackage package, string path, string mediaType)
    {
        if (!package.Entries.TryGetValue(path, out OdfPackageEntry? entry))
        {
            return new OdfPackageDebugEntry(path, mediaType, 0, compressed: false, encrypted: false, dirty: false, localHeaderOffset: -1, compressedDataOffset: -1, compressedSize: 0, compressionMethod: null);
        }

        OdfMmfEntryInfo? mmfEntry = entry.MmfEntry;
        return new OdfPackageDebugEntry(
            path,
            mediaType,
            entry.GetEstimatedSize(),
            mmfEntry is not null ? mmfEntry.CompressionMethod != 0 : entry.IsCompressed,
            entry.EncryptionInfo is not null,
            entry.IsModified,
            mmfEntry?.LocalHeaderOffset ?? -1,
            mmfEntry?.CompressedDataOffset ?? -1,
            mmfEntry?.CompressedSize ?? entry.GetEstimatedSize(),
            mmfEntry?.CompressionMethod);
    }
}

/// <summary>
/// 表示免 DOM 解析的原始二進位直改委派。
/// </summary>
/// <param name="input">原始 Entry 唯讀位元組區段</param>
/// <param name="output">用於寫入直改後內容的緩衝區寫入器</param>
/// <returns>若發生變更且需要寫回封裝則為 true，否則為 false</returns>
public delegate bool OdfRawEntryPatcher(ReadOnlySpan<byte> input, System.Buffers.IBufferWriter<byte> output);
