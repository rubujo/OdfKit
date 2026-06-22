using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OdfKit.Core;

/// <summary>
/// 管理 ODF 封裝中的媒體項目（如圖片），提供重複資料刪除與格式偵測功能。
/// </summary>
public class OdfMediaManager
{
    private readonly OdfPackage _package;
    // 將 SHA-256 圖片雜湊對應至其 ZIP 項目路徑（例如 "Pictures/image_hash.png" ）的字典
    private readonly Dictionary<string, string> _imageHashRegistry = new(StringComparer.Ordinal);
    private int _fallbackImageCounter;

    /// <summary>
    /// 初始化 <see cref="OdfMediaManager"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝執行個體</param>
    public OdfMediaManager(OdfPackage package)
    {
        _package = package;
        ScanExistingMedia();
    }

    private void ScanExistingMedia()
    {
        // 掃描資訊清單以尋找 Pictures/ 中現有的媒體
        foreach (var kvp in _package.Manifest)
        {
            if (kvp.Key.StartsWith("Pictures/", StringComparison.Ordinal))
            {
                try
                {
                    using var stream = _package.GetEntryStream(kvp.Key);
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    byte[] bytes = ms.ToArray();
                    string hash = ComputeSha256(bytes);
                    _imageHashRegistry[hash] = kvp.Key;
                }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Warn($"掃描現有媒體項目時失敗 '{kvp.Key}'： {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 插入圖片二進位資料。若圖片內容已存在，則會自動重用現有路徑，實現自動重複資料刪除。
    /// </summary>
    /// <param name="imageBytes">圖片的二進位內容</param>
    /// <param name="preferredName">偏好的檔名（若重複資料刪除未命中且未給定時會自動產生）</param>
    /// <returns>傳回該圖片在 ODF 封裝中的相對路徑（例如 "Pictures/image_hash.png" ）</returns>
    public string AddImage(byte[] imageBytes, string? preferredName = null)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            throw new ArgumentException("圖片資料不能為 null 或空。", nameof(imageBytes));
        }

        // 1. 計算 SHA-256 雜湊以進行重複資料刪除
        string hash = ComputeSha256(imageBytes);
        if (_imageHashRegistry.TryGetValue(hash, out string? existingPath))
        {
            OdfKitDiagnostics.Info($"重用現有的圖片項目： {existingPath}");
            return existingPath;
        }

        // 2. 從幻數偵測圖片格式
        DetectImageFormat(imageBytes, out string mimeType, out string extension);

        // 3. 解析項目路徑
        string entryPath;
        if (!string.IsNullOrWhiteSpace(preferredName))
        {
            string sanitizedName = Path.GetFileName(preferredName);
            if (!sanitizedName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                sanitizedName += extension;
            }
            entryPath = OdfPackage.SanitizeEntryName($"Pictures/{sanitizedName}");
        }
        else
        {
            // 預設後備路徑
            _fallbackImageCounter++;
            entryPath = OdfPackage.SanitizeEntryName($"Pictures/image_{_fallbackImageCounter}_{hash.Substring(0, 8)}{extension}");
        }

        // 解析名稱衝突
        int collisionCounter = 0;
        string finalPath = entryPath;
        while (_package.HasEntry(finalPath))
        {
            collisionCounter++;
            string dir = Path.GetDirectoryName(entryPath)?.Replace('\\', '/') ?? "Pictures";
            string nameWithoutExt = Path.GetFileNameWithoutExtension(entryPath);
            finalPath = OdfPackage.SanitizeEntryName($"{dir}/{nameWithoutExt}_{collisionCounter}{extension}");
        }

        // 4. 寫入封裝
        _package.WriteEntry(finalPath, imageBytes, mimeType);
        _imageHashRegistry[hash] = finalPath;

        OdfKitDiagnostics.Info($"插入新的圖片項目： {finalPath} ({mimeType})");
        return finalPath;
    }

    /// <summary>
    /// 根據檔案的幻數（Magic Bytes）偵測圖片格式。
    /// </summary>
    /// <param name="bytes">圖片的二進位內容</param>
    /// <param name="mimeType">輸出的 MIME 類型</param>
    /// <param name="extension">輸出的副檔名，包含前導句點</param>
    public static void DetectImageFormat(byte[] bytes, out string mimeType, out string extension)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            mimeType = "image/png";
            extension = ".png";
            return;
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            mimeType = "image/jpeg";
            extension = ".jpg";
            return;
        }

        if (bytes.Length >= 4 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
        {
            mimeType = "image/gif";
            extension = ".gif";
            return;
        }

        // WebP 格式 (RIFF....WEBP)
        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 && // RIFF
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) // WEBP
        {
            mimeType = "image/webp";
            extension = ".webp";
            return;
        }

        // BMP 格式 (BM)
        if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D)
        {
            mimeType = "image/bmp";
            extension = ".bmp";
            return;
        }

        // TIFF 格式 (II* / MM*)
        if (bytes.Length >= 4 &&
            ((bytes[0] == 0x49 && bytes[1] == 0x49 && bytes[2] == 0x2A && bytes[3] == 0x00) || // Little Endian
             (bytes[0] == 0x4D && bytes[1] == 0x4D && bytes[2] == 0x00 && bytes[3] == 0x2A)))  // Big Endian
        {
            mimeType = "image/tiff";
            extension = ".tiff";
            return;
        }

        // EMF 格式（開頭 0x01 0x00 0x00 0x00，且偏移 40 處為 " EMF"）
        if (bytes.Length >= 44 &&
            bytes[0] == 0x01 && bytes[1] == 0x00 && bytes[2] == 0x00 && bytes[3] == 0x00 &&
            bytes[40] == 0x20 && bytes[41] == 0x45 && bytes[42] == 0x4D && bytes[43] == 0x46)
        {
            mimeType = "image/x-emf";
            extension = ".emf";
            return;
        }

        // WMF 格式
        if (bytes.Length >= 4 &&
            bytes[0] == 0xD7 && bytes[1] == 0xCD && bytes[2] == 0xC6 && bytes[3] == 0x9A)
        {
            mimeType = "image/x-wmf";
            extension = ".wmf";
            return;
        }
        if (bytes.Length >= 10 &&
            ((bytes[0] == 0x01 && bytes[1] == 0x00) || (bytes[0] == 0x09 && bytes[1] == 0x00)) &&
            bytes[2] == 0x00 && bytes[3] == 0x00)
        {
            mimeType = "image/x-wmf";
            extension = ".wmf";
            return;
        }

        // SVG 簡單檢查
        if (IsSvg(bytes))
        {
            mimeType = "image/svg+xml";
            extension = ".svg";
            return;
        }

        // 預設後備
        mimeType = "application/octet-stream";
        extension = ".bin";
    }

    private static bool IsSvg(byte[] bytes)
    {
        // 讀取前 512 位元組作為字串以掃描 svg 簽章
        int len = Math.Min(bytes.Length, 512);
        try
        {
            string text = Encoding.UTF8.GetString(bytes, 0, len);
            return text.Contains("<svg") || text.Contains("xmlns=\"http://www.w3.org/2000/svg\"");
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        byte[] hashBytes = sha.ComputeHash(bytes);
        var sb = new StringBuilder(hashBytes.Length * 2);
        foreach (byte b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}

