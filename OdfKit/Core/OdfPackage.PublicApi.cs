using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OdfKit.Compliance;

namespace OdfKit.Core;
/// <summary>
/// Adds public entry management APIs for ODF packages.
/// 提供 ODF 封裝的公開專案管理 API。
/// </summary>

public sealed partial class OdfPackage
{
    #region Public API

    /// <summary>
    /// Returns whether this instance is entry is present.
    /// 檢查封裝中是否包含指定名稱的專案。
    /// </summary>
    /// <param name="name">專案的相對路徑名稱</param>
    /// <returns>若專案存在則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    public bool HasEntry(string name)
        => OdfPackageEntryAccessEngine.HasEntry(EntryCollaborators, name);

    /// <summary>
    /// Provides the OdfPackageEntryInfo API.
    /// 提供 ODF 封裝中實體專案的基本資訊。
    /// </summary>
    public class OdfPackageEntryInfo
    {
        /// <summary>
        /// Gets the Path value.
        /// 取得專案的相對路徑。
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Performs odf package entry info.
        /// 初始化 <see cref="OdfPackageEntryInfo"/> 類別的新執行個體。
        /// </summary>
        /// <param name="path">專案的相對路徑</param>
        public OdfPackageEntryInfo(string path) => Path = path;
    }

    /// <summary>
    /// Gets entries.
    /// 取得封裝中所有實體專案的資訊集合。
    /// </summary>
    /// <returns>所有專案的資訊集合</returns>
    public IEnumerable<OdfPackageEntryInfo> GetEntries()
        => OdfPackageEntryAccessEngine.GetEntries(EntryCollaborators);

    /// <summary>
    /// Reads entry.
    /// 讀取指定路徑專案的完整內容位元組。
    /// </summary>
    /// <param name="path">專案的相對路徑名稱</param>
    /// <returns>專案的位元組陣列內容</returns>
    public byte[] ReadEntry(string path)
        => OdfPackageEntryAccessEngine.ReadEntry(EntryCollaborators, path);

    /// <summary>
    /// Performs the Save operation.
    /// 將目前 ODF 封裝儲存到指定的目標資料流中。
    /// </summary>
    public void Save(Stream stream) => Save(stream, null);

    /// <summary>
    /// Full overload of Save that accepts stream and options.
    /// Save 完整多載：接受 stream 與 options。
    /// </summary>
    public void Save(Stream stream, OdfSaveOptions? options)
    {
        SaveToStream(stream, options);
    }

    /// <summary>
    /// Gets entry stream.
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
    /// Writes entry.
    /// 將指定的位元組內容寫入或覆寫封裝中的專案。
    /// </summary>
    public void WriteEntry(string name, byte[] content) => WriteEntry(name, content, null);

    /// <summary>
    /// Short overload of WriteEntry that accepts name, content, and mediaType; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name、content 與 mediaType；其餘可選參數使用預設值並轉呼叫最長 WriteEntry 多載。
    /// </summary>
    public void WriteEntry(string name, byte[] content, string? mediaType)
        => OdfPackageEntryAccessEngine.WriteEntry(EntryCollaborators, name, content, mediaType);

    /// <summary>
    /// Writes entry.
    /// 將指定的資料流內容寫入或覆寫封裝中的專案。
    /// </summary>
    public void WriteEntry(string name, Stream contentStream) => WriteEntry(name, contentStream, null);

    /// <summary>
    /// Short overload of WriteEntry that accepts name, contentStream, and mediaType; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name、contentStream 與 mediaType；其餘可選參數使用預設值並轉呼叫最長 WriteEntry 多載。
    /// </summary>
    public void WriteEntry(string name, Stream contentStream, string? mediaType)
        => OdfPackageEntryAccessEngine.WriteEntry(EntryCollaborators, name, contentStream, mediaType);

    /// <summary>
    /// Adds entry.
    /// 將指定的位元組內容新增至封裝；若同名專案已存在，則覆寫該專案。
    /// </summary>
    public void AddEntry(string name, byte[] content) => WriteEntry(name, content, null);

    /// <summary>
    /// Short overload of AddEntry that accepts name, content, and mediaType; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name、content 與 mediaType；其餘可選參數使用預設值並轉呼叫最長 AddEntry 多載。
    /// </summary>
    public void AddEntry(string name, byte[] content, string? mediaType)
        => WriteEntry(name, content, mediaType);

    /// <summary>
    /// Adds entry.
    /// 將指定的資料流內容新增至封裝；若同名專案已存在，則覆寫該專案。
    /// </summary>
    public void AddEntry(string name, Stream contentStream) => WriteEntry(name, contentStream, null);

    /// <summary>
    /// Short overload of AddEntry that accepts name, contentStream, and mediaType; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name、contentStream 與 mediaType；其餘可選參數使用預設值並轉呼叫最長 AddEntry 多載。
    /// </summary>
    public void AddEntry(string name, Stream contentStream, string? mediaType)
        => WriteEntry(name, contentStream, mediaType);

    /// <summary>
    /// Removes the specified entry from the package.
    /// 從封裝中移除指定的專案。
    /// </summary>
    /// <param name="name">The relative package entry path to remove. / 要移除的專案相對路徑名稱。</param>
    /// <returns><see langword="true"/> if an entry, manifest item, or entry-order item was removed; otherwise, <see langword="false"/>. / 若已移除 entry、manifest 項目或 entry order 項目則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveEntry(string name)
        => OdfPackageEntryAccessEngine.RemoveEntry(EntryCollaborators, name);

    /// <summary>
    /// Performs prune unused media.
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
    /// Sets mime type.
    /// 設定 ODF 封裝的主要 MIME 媒體類型。
    /// </summary>
    /// <param name="mimetype">媒體類型字串</param>
    public void SetMimeType(string mimetype)
        => OdfPackageEntryAccessEngine.SetMimeType(EntryCollaborators, mimetype);

    #endregion

    #region Embedded Objects Extraction

    /// <summary>
    /// Gets embedded objects.
    /// 取得此封裝中所內嵌的 ODF 物件資料夾路徑清單。
    /// </summary>
    /// <returns>內嵌物件路徑的集合</returns>
    public IEnumerable<string> GetEmbeddedObjects()
        => OdfPackageEntryAccessEngine.GetEmbeddedObjects(EntryCollaborators);

    /// <summary>
    /// Extracts object stream.
    /// 擷取內嵌物件的主要內容 XML 資料流。
    /// </summary>
    /// <param name="objectName">內嵌物件的路徑名稱</param>
    /// <returns>內嵌物件內容的資料流</returns>
    public Stream ExtractObjectStream(string objectName)
        => OdfPackageEntryAccessEngine.ExtractObjectStream(EntryCollaborators, objectName);

    /// <summary>
    /// Performs raw entry patch.
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

            if (entryName != OdfSignerConstants.SignaturePath && entryName != "META-INF/manifest.xml")
            {
                RemoveOutdatedSignatures();
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Performs dump vfs layout.
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
        /// <summary>
        /// Short overload of SimpleBufferWriter that uses default values for all optional parameters and forwards to the full overload.
        /// 便利多載：SimpleBufferWriter 的所有可選參數使用預設值並轉呼叫最長多載。
        /// </summary>
        public SimpleBufferWriter() : this(256) { }


        /// <summary>
        /// Full overload of SimpleBufferWriter that accepts initialCapacity.
        /// SimpleBufferWriter 完整多載：接受 initialCapacity。
        /// </summary>
        public SimpleBufferWriter(int initialCapacity)
        {
            _buffer = new byte[initialCapacity];
        }


        /// <summary>
        /// Gets the written portion of the buffer as a read-only span.
        /// 取得緩衝區已寫入部分的唯讀跨度。
        /// </summary>
        public ReadOnlySpan<byte> WrittenReadOnlySpan => _buffer.AsSpan(0, _written);

        /// <summary>
        /// Full overload of Advance that accepts count.
        /// Advance 完整多載：接受 count。
        /// </summary>
        public void Advance(int count)
        {
            if (count < 0)
                throw new ArgumentException(nameof(count));
            if (_written + count > _buffer.Length)
                throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfPackage_CannotAdvancePastBufferedLength"));
            _written += count;
        }
        /// <summary>
        /// Short overload of GetMemory that uses default values for all optional parameters and forwards to the full overload.
        /// 便利多載：GetMemory 的所有可選參數使用預設值並轉呼叫最長多載。
        /// </summary>
        public Memory<byte> GetMemory() => GetMemory(0);


        /// <summary>
        /// Full overload of GetMemory that accepts sizeHint.
        /// GetMemory 完整多載：接受 sizeHint。
        /// </summary>
        public Memory<byte> GetMemory(int sizeHint)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsMemory(_written);
        }

        /// <summary>
        /// Short overload of GetSpan that uses default values for all optional parameters and forwards to the full overload.
        /// 便利多載：GetSpan 的所有可選參數使用預設值並轉呼叫最長多載。
        /// </summary>
        public Span<byte> GetSpan() => GetSpan(0);


        /// <summary>
        /// Full overload of GetSpan that accepts sizeHint.
        /// GetSpan 完整多載：接受 sizeHint。
        /// </summary>
        public Span<byte> GetSpan(int sizeHint)
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

    /// <summary>
    /// Short overload of ToString that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：ToString 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
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
/// Performs odf raw entry patcher.
/// 表示免 DOM 解析的原始二進位直改委派。
/// </summary>
/// <param name="input">原始 Entry 唯讀位元組區段</param>
/// <param name="output">用於寫入直改後內容的緩衝區寫入器</param>
/// <returns>若發生變更且需要寫回封裝則為 true，否則為 false</returns>
public delegate bool OdfRawEntryPatcher(ReadOnlySpan<byte> input, System.Buffers.IBufferWriter<byte> output);
