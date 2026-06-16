using System;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    #region Initialization & Loading

    private void InitializeLoad()
    {
        if (_underlyingStream == null)
            throw new InvalidOperationException("No input stream available.");

        // 嗅探簽章：檢查是否為 ZIP（PK\x03\x04）
        byte[] signature = new byte[4];
        int bytesRead = 0;
        if (_underlyingStream.CanSeek)
        {
            long initialPosition = _underlyingStream.Position;
            _underlyingStream.Position = 0;
            bytesRead = ReadAll(_underlyingStream, signature, 0, signature.Length);
            _underlyingStream.Position = initialPosition;
        }
        else
        {
            bytesRead = ReadAll(_underlyingStream, signature, 0, signature.Length);
        }

        bool isZip = bytesRead == 4 &&
                     signature[0] == 0x50 &&
                     signature[1] == 0x4B &&
                     signature[2] == 0x03 &&
                     signature[3] == 0x04;

        if (!isZip)
        {
            _isFlatXml = true;
            InitializeFlatXml(signature, bytesRead);
            return;
        }

        if (!_underlyingStream.CanSeek)
        {
            // 若為 ZIP 且串流不可搜尋，複製到可搜尋的 MemoryStream
            // 因為 ZipArchive 需要可搜尋串流才能讀取中央目錄
            var ms = new MemoryStream();
            ms.Write(signature, 0, bytesRead);
            _underlyingStream.CopyTo(ms);
            ms.Position = 0;
            if (!_leaveOpen)
            {
                _underlyingStream.Dispose();
            }
            _underlyingStream = ms;
        }

        // 若需要，在 .NET Standard 2.0 註冊 ZIP 檔名的 CodePages
#if NETSTANDARD2_0
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
            catch
            {
                // 若平台不支援或缺少參考則靜默略過
            }
#endif

        // 開啟 ZIP 封存
        _archive = new ZipArchive(_underlyingStream, ZipArchiveMode.Read, _leaveOpen, Encoding.UTF8);

        // Zip DoS 防禦：計算項目數量
        if (_archive.Entries.Count > _loadOptions.MaxZipEntries)
        {
            throw new SecurityException($"Zip archive contains too many entries ({_archive.Entries.Count} > {_loadOptions.MaxZipEntries}). Potential Zip DoS attack.");
        }

        long totalUncompressedSize = 0;

        foreach (var entry in _archive.Entries)
        {
            // 清理並檢查 Zip Slip。若不安全，仍以原始名稱存入記憶體
            // 以便合規驗證器回報（Fatal ODF0200/ODF0201 問題），
            // 但之後透過 SanitizeEntryName 存取此項目將拋出 SecurityException
            string name;
            try
            {
                name = SanitizeEntryName(entry.FullName);
            }
            catch (SecurityException)
            {
                name = entry.FullName;
            }

            // Zip DoS 防禦：項目大小
            if (entry.Length > _loadOptions.MaxEntrySize)
            {
                throw new SecurityException($"Zip entry '{name}' exceeds size limit ({entry.Length} > {_loadOptions.MaxEntrySize} bytes).");
            }

            totalUncompressedSize += entry.Length;
            if (totalUncompressedSize > _loadOptions.MaxTotalUncompressedSize)
            {
                throw new SecurityException($"Zip archive total uncompressed size exceeds limit ({totalUncompressedSize} > {_loadOptions.MaxTotalUncompressedSize} bytes).");
            }

            byte[] entryBytes;
            using (var entryStream = entry.Open())
            using (var ms = new MemoryStream())
            {
                entryStream.CopyTo(ms);
                entryBytes = ms.ToArray();
            }
            var pkgEntry = new OdfPackageEntry(name, entryBytes);
            if (entry.CompressedLength == entry.Length && entry.Length > 0)
            {
                pkgEntry.IsCompressed = false;
            }

            // 判斷是否以未壓縮方式儲存
            bool wasStored = false;
            try
            {
                var fieldInfo = typeof(ZipArchiveEntry).GetField("_compressionMethod", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?? typeof(ZipArchiveEntry).GetField("m_compressionMethod", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    var val = fieldInfo.GetValue(entry);
                    if (val != null)
                    {
                        int intVal = Convert.ToInt32(val);
                        wasStored = (intVal == 0);
                    }
                }
                else
                {
                    OdfKitDiagnostics.Warn($"[OdfPackage] 無法反射取得 ZipArchiveEntry 壓縮方式欄位 ( .NET {Environment.Version} )；讀取時將以 CompressedLength == Length 作為判斷基準。");
                    wasStored = (entry.CompressedLength == entry.Length);
                }
            }
            catch
            {
                wasStored = (entry.CompressedLength == entry.Length);
            }
            pkgEntry.WasStoredInZip = wasStored;
            if (_entries.ContainsKey(name))
            {
                _duplicateEntryNames.Add(name);
            }
            _entries[name] = pkgEntry;
            if (!_entryOrder.Contains(name))
            {
                _entryOrder.Add(name);
            }
        }

        // 載入 mimetype
        if (_entries.TryGetValue("mimetype", out var mimeEntry))
        {
            using var reader = new StreamReader(mimeEntry.OpenReader(), Encoding.UTF8);
            _mimetype = reader.ReadToEnd().Trim();
        }
        else if (_loadOptions.ValidateMimeType)
        {
            throw new InvalidDataException("Invalid ODF package: 'mimetype' file is missing.");
        }

        // 載入 manifest
        LoadManifest();

        if (_loadOptions.Password != null || _loadOptions.CryptographyProvider != null)
        {
            OdfEncryption.Decrypt(this, _loadOptions.Password ?? string.Empty);
        }

        LoadRdfMetadata();
    }

    #endregion
}
