using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using OdfKit.Compliance;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    private bool _inTransaction;
    internal bool InTransaction => _inTransaction;
    private List<IUndoOperation> _undoLog = new();

    internal static int DirectCentralDirectoryWriteCountForTests { get; private set; }

    internal static int DirectEndOfCentralDirectoryWriteCountForTests { get; private set; }

    private interface IUndoOperation
    {
        void Undo(OdfPackage package);
    }

    private class UndoSetEntry : IUndoOperation
    {
        public string Key { get; }
        public OdfPackageEntry? OldValue { get; }
        public bool HadOldValue { get; }

        public UndoSetEntry(string key, OdfPackageEntry? oldValue, bool hadOldValue)
        {
            Key = key;
            OldValue = oldValue;
            HadOldValue = hadOldValue;
        }

        public void Undo(OdfPackage package)
        {
            if (HadOldValue)
            {
                package._entries[Key] = OldValue!;
            }
            else
            {
                if (package._entries.TryGetValue(Key, out var entry))
                {
                    entry.Dispose();
                }
                package._entries.Remove(Key);
            }
        }
    }

    private class UndoRemoveEntry : IUndoOperation
    {
        public string Key { get; }
        public OdfPackageEntry OldValue { get; }

        public UndoRemoveEntry(string key, OdfPackageEntry oldValue)
        {
            Key = key;
            OldValue = oldValue;
        }

        public void Undo(OdfPackage package)
        {
            package._entries[Key] = OldValue;
        }
    }

    private class UndoSetManifest : IUndoOperation
    {
        public string Key { get; }
        public string? OldValue { get; }
        public bool HadOldValue { get; }

        public UndoSetManifest(string key, string? oldValue, bool hadOldValue)
        {
            Key = key;
            OldValue = oldValue;
            HadOldValue = hadOldValue;
        }

        public void Undo(OdfPackage package)
        {
            if (HadOldValue)
            {
                package._manifest[Key] = OldValue!;
            }
            else
            {
                package._manifest.Remove(Key);
            }
        }
    }

    private class UndoRemoveManifest : IUndoOperation
    {
        public string Key { get; }
        public string OldValue { get; }

        public UndoRemoveManifest(string key, string oldValue)
        {
            Key = key;
            OldValue = oldValue;
        }

        public void Undo(OdfPackage package)
        {
            package._manifest[Key] = OldValue;
        }
    }

    private class UndoSetEntryOrder : IUndoOperation
    {
        private readonly List<string> _oldOrder;

        public UndoSetEntryOrder(List<string> oldOrder)
        {
            _oldOrder = oldOrder;
        }

        public void Undo(OdfPackage package)
        {
            package._entryOrder.Clear();
            package._entryOrder.AddRange(_oldOrder);
        }
    }

    private class UndoableDictionary<TKey, TValue> : IDictionary<TKey, TValue> where TKey : notnull
    {
        private readonly OdfPackage _package;
        private readonly IDictionary<TKey, TValue> _inner;
        private readonly Action<TKey, TValue?, bool> _onSet;
        private readonly Action<TKey, TValue> _onRemove;

        public UndoableDictionary(OdfPackage package, IDictionary<TKey, TValue> inner, Action<TKey, TValue?, bool> onSet, Action<TKey, TValue> onRemove)
        {
            _package = package;
            _inner = inner;
            _onSet = onSet;
            _onRemove = onRemove;
        }

        public TValue this[TKey key]
        {
            get => _inner[key];
            set
            {
                if (_package._inTransaction)
                {
                    bool existed = _inner.TryGetValue(key, out var old);
                    _onSet(key, old, existed);
                }
                _inner[key] = value;
            }
        }

        public ICollection<TKey> Keys => _inner.Keys;
        public ICollection<TValue> Values => _inner.Values;
        public int Count => _inner.Count;
        public bool IsReadOnly => _inner.IsReadOnly;

        public void Add(TKey key, TValue value)
        {
            if (_package._inTransaction)
            {
                _onSet(key, default!, false);
            }
            _inner.Add(key, value);
        }

        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        public void Clear()
        {
            if (_package._inTransaction)
            {
                foreach (var kvp in _inner)
                {
                    _onRemove(kvp.Key, kvp.Value);
                }
            }
            _inner.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item) => _inner.Contains(item);
        public bool ContainsKey(TKey key) => _inner.ContainsKey(key);
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _inner.GetEnumerator();

        public bool Remove(TKey key)
        {
            if (_package._inTransaction && _inner.TryGetValue(key, out var old))
            {
                _onRemove(key, old);
            }
            return _inner.Remove(key);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

#if NET10_0_OR_GREATER
        public bool TryGetValue(TKey key, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out TValue value) => _inner.TryGetValue(key, out value);
#else
        public bool TryGetValue(TKey key, out TValue value) => _inner.TryGetValue(key, out value);
#endif

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _inner.GetEnumerator();
    }

    private class UndoableList<T> : IList<T>
    {
        private readonly OdfPackage _package;
        private readonly IList<T> _inner;
        private readonly Action<List<T>> _onBeforeChange;

        public UndoableList(OdfPackage package, IList<T> inner, Action<List<T>> onBeforeChange)
        {
            _package = package;
            _inner = inner;
            _onBeforeChange = onBeforeChange;
        }

        private void LogChange()
        {
            if (_package._inTransaction)
            {
                _onBeforeChange(new List<T>(_inner));
            }
        }

        public T this[int index]
        {
            get => _inner[index];
            set { LogChange(); _inner[index] = value; }
        }

        public int Count => _inner.Count;
        public bool IsReadOnly => _inner.IsReadOnly;

        public void Add(T item) { LogChange(); _inner.Add(item); }
        public void Clear() { LogChange(); _inner.Clear(); }
        public bool Contains(T item) => _inner.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);
        public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();
        public int IndexOf(T item) => _inner.IndexOf(item);
        public void Insert(int index, T item) { LogChange(); _inner.Insert(index, item); }
        public bool Remove(T item) { LogChange(); return _inner.Remove(item); }
        public void RemoveAt(int index) { LogChange(); _inner.RemoveAt(index); }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _inner.GetEnumerator();
    }

    /// <summary>
    /// 開始一個全新的沙盒交易，所有變更將在交易被認可前予以隔離，完全在記憶體中維護 O(1) 的虛擬 VFS 變更集。
    /// </summary>
    internal void BeginTransaction()
    {
        _lock.Wait();
        try
        {
            if (_inTransaction)
                throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfPackage_TransactionAlreadyInProgress"));

            // 建立實體磁碟交易日誌備份
            if (!string.IsNullOrEmpty(FilePath) && File.Exists(FilePath))
            {
                string journalPath = FilePath + ".journal";
                try
                {
                    File.Copy(FilePath, journalPath, true);
                    using (var fs = new FileStream(journalPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                    {
                        fs.Flush(true); // fsync 強制刷入磁碟
                    }
                }
                catch (Exception ex)
                {
                    _undoLog.Clear();
                    _inTransaction = false;
                    throw new IOException(OdfLocalizer.GetMessage("Err_OdfPackage_JournalCreateFailed"), ex);
                }
            }

            _undoLog.Clear();
            _inTransaction = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 認可並提交沙盒交易，一次性將所有隔離變更套用回底層的物理 VFS 狀態中。
    /// </summary>
    internal void CommitTransaction()
    {
        _lock.Wait();
        try
        {
            if (!_inTransaction)
                return;

            // 提交變更時，強制將底層檔案串流寫入磁碟 (fsync)
            if (_underlyingStream is FileStream fs)
            {
                try
                {
                    fs.Flush(true);
                }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Warn($"[OdfPackage] 提交交易時強制刷入磁碟失敗: {ex.Message}");
                }
            }

            // 刪除交易日誌
            if (!string.IsNullOrEmpty(FilePath))
            {
                string journalPath = FilePath + ".journal";
                if (File.Exists(journalPath))
                {
                    try
                    {
                        File.Delete(journalPath);
                    }
                    catch (Exception ex)
                    {
                        OdfKitDiagnostics.Warn($"[OdfPackage] 刪除交易日誌 {journalPath} 失敗: {ex.Message}");
                    }
                }
            }

            _undoLog.Clear();
            _inTransaction = false;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 回滾並丟棄沙盒交易中的所有變更，並主動處置新建置專案所佔用的資源，完全不留痕跡。
    /// </summary>
    internal void RollbackTransaction()
    {
        _lock.Wait();
        try
        {
            if (!_inTransaction)
                return;

            // 反向重播撤銷日誌 (Undo Log)
            for (int i = _undoLog.Count - 1; i >= 0; i--)
            {
                _undoLog[i].Undo(this);
            }

            // 實體磁碟還原
            if (!string.IsNullOrEmpty(FilePath))
            {
                string journalPath = FilePath + ".journal";
                if (File.Exists(journalPath))
                {
                    try
                    {
                        if (_underlyingStream != null && _underlyingStream.CanWrite && _underlyingStream.CanSeek)
                        {
                            using (var journalStream = new FileStream(journalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                _underlyingStream.Position = 0;
                                _underlyingStream.SetLength(0);
                                journalStream.CopyTo(_underlyingStream);
                                _underlyingStream.Flush();
                            }
                            if (_underlyingStream is FileStream fs)
                            {
                                fs.Flush(true); // fsync 強制刷入磁碟
                            }
                        }
                        File.Delete(journalPath);
                    }
                    catch (Exception ex)
                    {
                        OdfKitDiagnostics.Warn($"[OdfPackage] 回滾交易日誌實體檔案失敗: {ex.Message}");
                    }
                }
            }

            _undoLog.Clear();
            _inTransaction = false;
            OnRollback?.Invoke();
        }
        finally
        {
            _lock.Release();
        }
    }

    internal static bool TryIncrementalZipAppend(
        OdfPackage package,
        OdfPackage.OdfPackageSaveCollaborators ctx,
        Stream underlying,
        bool includeRdfMetadata)
    {
        try
        {
            if (package.MmfEntries == null)
                return false;

            // 1. 尋找 EOCD 以便得到 cdOffset
            long eocdOffset = OdfZipDirectoryParser.FindEocdOffset(underlying);
            if (eocdOffset < 0)
                return false;

            underlying.Position = eocdOffset + 16;
            using (var reader = new BinaryReader(underlying, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                uint oldCdOffset = reader.ReadUInt32();

                var modifiedEntries = new List<OdfPackageEntry>();

                // 2. 確定哪些被修改或新增
                foreach (var kvp in ctx.Entries)
                {
                    var entry = kvp.Value;
                    if (entry.IsModifiedForZipAppend)
                    {
                        entry.EnsureBytesLoaded();
                        modifiedEntries.Add(entry);
                    }
                }

                // 3. 處理 STORED 條目的原位 Patch 優化
                var remainingToAppend = new List<OdfPackageEntry>();
                foreach (var entry in modifiedEntries)
                {
                    if (entry.MmfEntry != null && entry.MmfEntry.CompressionMethod == 0)
                    {
                        byte[] data = entry.GetCachedBytes() ?? Array.Empty<byte>();
                        if (data.Length <= entry.MmfEntry.CompressedSize)
                        {
                            // 進行原位覆寫
                            underlying.Position = entry.MmfEntry.CompressedDataOffset;
                            underlying.Write(data, 0, data.Length);

                            // 填充空白
                            int pad = (int)(entry.MmfEntry.CompressedSize - data.Length);
                            if (pad > 0)
                            {
                                byte[] spaces = new byte[pad];
                                for (int i = 0; i < pad; i++)
                                    spaces[i] = 32; // 空白字元
                                underlying.Write(spaces, 0, pad);
                            }

                            // 計算新 CRC32
                            uint crc = OdfCrc32.Compute(data);

                            // 更新原 LFH 中的 CRC32、CompressedSize、UncompressedSize
                            underlying.Position = entry.MmfEntry.LocalHeaderOffset + 14;
                            WriteUInt32LittleEndian(underlying, crc);
                            WriteUInt32LittleEndian(underlying, (uint)data.Length);
                            WriteUInt32LittleEndian(underlying, (uint)data.Length);

                            // 更新 mmfEntry 的屬性，以便隨後的 CD 寫入使用新屬性
                            var newMmfEntry = new OdfMmfEntryInfo(
                                entry.Name,
                                entry.MmfEntry.CompressedDataOffset,
                                data.Length,
                                data.Length,
                                0,
                                crc,
                                entry.MmfEntry.LocalHeaderOffset,
                                entry.MmfEntry.Flags,
                                entry.MmfEntry.TimeDate
                            );
                            package.MmfEntries[entry.Name] = newMmfEntry;
                            continue;
                        }
                    }
                    remainingToAppend.Add(entry);
                }

                // 4. 追加其餘被修改/新增的 entries
                underlying.Position = oldCdOffset;
                var appendedHeaders = new Dictionary<string, uint>(StringComparer.Ordinal);
                var appendedCrcs = new Dictionary<string, uint>(StringComparer.Ordinal);
                var appendedCompSizes = new Dictionary<string, uint>(StringComparer.Ordinal);
                var appendedUncompSizes = new Dictionary<string, uint>(StringComparer.Ordinal);
                var appendedMethods = new Dictionary<string, ushort>(StringComparer.Ordinal);

                using (var writer = new BinaryWriter(underlying, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    foreach (var entry in remainingToAppend)
                    {
                        byte[] data = entry.GetCachedBytes() ?? Array.Empty<byte>();
                        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(entry.Name);
                        uint crc = OdfCrc32.Compute(data);

                        byte[] writeBytes;
                        ushort compMethod;

                        if (entry.IsCompressed)
                        {
                            using (var ms = new MemoryStream())
                            {
                                using (var ds = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionLevel.Fastest, true))
                                {
                                    ds.Write(data, 0, data.Length);
                                }
                                writeBytes = ms.ToArray();
                            }
                            compMethod = 8;
                        }
                        else
                        {
                            writeBytes = data;
                            compMethod = 0;
                        }

                        uint lfhOffset = (uint)underlying.Position;
                        WriteLfh(writer, entry.Name, compMethod, crc, (uint)writeBytes.Length, (uint)data.Length, nameBytes);
                        writer.Write(writeBytes);

                        appendedHeaders[entry.Name] = lfhOffset;
                        appendedCrcs[entry.Name] = crc;
                        appendedCompSizes[entry.Name] = (uint)writeBytes.Length;
                        appendedUncompSizes[entry.Name] = (uint)data.Length;
                        appendedMethods[entry.Name] = compMethod;
                    }

                    // 5. 寫入全新的 Central Directory
                    uint newCdOffset = (uint)underlying.Position;
                    ushort writtenEntryCount = 0;

                    foreach (var name in package._entryOrder)
                    {
                        if (!ctx.Entries.ContainsKey(name))
                            continue;

                        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(name);

                        // 判斷是否為追加項目
                        if (appendedHeaders.TryGetValue(name, out uint lfhOffset))
                        {
                            WriteCdEntry(
                                underlying,
                                name,
                                appendedMethods[name],
                                appendedCrcs[name],
                                appendedCompSizes[name],
                                appendedUncompSizes[name],
                                lfhOffset,
                                nameBytes,
                                0x0800,
                                0x58212100
                            );
                            writtenEntryCount++;
                        }
                        // 判斷是否為原位 Patch 項目（已經更新在 MmfEntries 中）
                        else if (package.MmfEntries.TryGetValue(name, out var mmfEntry))
                        {
                            WriteCdEntry(
                                underlying,
                                name,
                                mmfEntry.CompressionMethod,
                                mmfEntry.Crc32,
                                (uint)mmfEntry.CompressedSize,
                                (uint)mmfEntry.UncompressedSize,
                                (uint)mmfEntry.LocalHeaderOffset,
                                nameBytes,
                                mmfEntry.Flags,
                                mmfEntry.TimeDate
                            );
                            writtenEntryCount++;
                        }
                    }

                    uint cdSize = (uint)underlying.Position - newCdOffset;

                    // 6. 寫入 EOCD
                    WriteEocd(underlying, writtenEntryCount, cdSize, newCdOffset);
                }

                underlying.SetLength(underlying.Position);
                if (underlying is FileStream fs)
                {
                    fs.Flush(true);
                }
                else
                {
                    underlying.Flush();
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Warn($"[OdfPackage] 增量追加儲存優化失敗，將降級為全量覆寫。原因: {ex.Message}");
            return false;
        }
    }

    private static void WriteLfh(BinaryWriter writer, string name, ushort compMethod, uint crc, uint compSize, uint uncompSize, byte[] nameBytes)
    {
        writer.Write(0x04034b50); // signature
        writer.Write((ushort)20); // version needed to extract (2.0)
        writer.Write((ushort)0x0800);  // flags (UTF-8)
        writer.Write(compMethod);
        writer.Write((uint)0x58212100); // time and date (2024-01-01 12:00)
        writer.Write(crc);
        writer.Write(compSize);
        writer.Write(uncompSize);
        writer.Write((ushort)nameBytes.Length);
        writer.Write((ushort)0); // extra field length
        writer.Write(nameBytes);
    }

    private static void WriteCdEntry(Stream stream, string name, ushort compMethod, uint crc, uint compSize, uint uncompSize, uint lfhOffset, byte[] nameBytes, ushort flags, uint timeDate)
    {
        WriteUInt32LittleEndian(stream, 0x02014b50); // signature
        WriteUInt16LittleEndian(stream, 20); // version made by (2.0)
        WriteUInt16LittleEndian(stream, 20); // version needed to extract (2.0)
        WriteUInt16LittleEndian(stream, flags);
        WriteUInt16LittleEndian(stream, compMethod);
        WriteUInt32LittleEndian(stream, timeDate);
        WriteUInt32LittleEndian(stream, crc);
        WriteUInt32LittleEndian(stream, compSize);
        WriteUInt32LittleEndian(stream, uncompSize);
        WriteUInt16LittleEndian(stream, (ushort)nameBytes.Length);
        WriteUInt16LittleEndian(stream, 0); // extra field length
        WriteUInt16LittleEndian(stream, 0); // comment length
        WriteUInt16LittleEndian(stream, 0); // disk start
        WriteUInt16LittleEndian(stream, 0); // internal attr
        WriteUInt32LittleEndian(stream, 0); // external attr
        WriteUInt32LittleEndian(stream, lfhOffset);
        stream.Write(nameBytes, 0, nameBytes.Length);
        DirectCentralDirectoryWriteCountForTests++;
    }

    private static void WriteEocd(Stream stream, ushort entryCount, uint cdSize, uint cdOffset)
    {
        WriteUInt32LittleEndian(stream, 0x06054b50); // signature
        WriteUInt16LittleEndian(stream, 0); // disk number
        WriteUInt16LittleEndian(stream, 0); // disk where CD starts
        WriteUInt16LittleEndian(stream, entryCount); // record count on disk
        WriteUInt16LittleEndian(stream, entryCount); // total record count
        WriteUInt32LittleEndian(stream, cdSize);
        WriteUInt32LittleEndian(stream, cdOffset);
        WriteUInt16LittleEndian(stream, 0); // comment length
        DirectEndOfCentralDirectoryWriteCountForTests++;
    }

    private static void WriteUInt16LittleEndian(Stream stream, ushort value)
    {
#if NETSTANDARD2_0
        byte[] buffer = ArrayPool<byte>.Shared.Rent(sizeof(ushort));
        try
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(0, sizeof(ushort)), value);
            stream.Write(buffer, 0, sizeof(ushort));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
#else
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        stream.Write(buffer);
#endif
    }

    private static void WriteUInt32LittleEndian(Stream stream, uint value)
    {
#if NETSTANDARD2_0
        byte[] buffer = ArrayPool<byte>.Shared.Rent(sizeof(uint));
        try
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, sizeof(uint)), value);
            stream.Write(buffer, 0, sizeof(uint));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
#else
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        stream.Write(buffer);
#endif
    }
}
