using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using OdfKit.Compliance;
namespace OdfKit.Core;

/// <summary>
/// Provides the OdfMediaManager API.
/// 管理 ODF 封裝中的媒體專案（如圖片），提供重複資料刪除與格式偵測功能。
/// </summary>
public class OdfMediaManager
{
    /// <summary>
    /// The canonical package entry path prefix used for embedded media (e.g. images).
    /// 內嵌媒體（如圖片）於封裝中使用的規範項目路徑前綴。
    /// </summary>
    public const string PicturesEntryPrefix = "Pictures/";

    private readonly OdfPackage _package;
    // 將 SHA-256 圖片雜湊對應至其 ZIP 專案路徑（例如 "Pictures/image_hash.png" ）的字典
    private readonly Dictionary<string, string> _imageHashRegistry = new(StringComparer.Ordinal);
    private int _fallbackImageCounter;

    /// <summary>
    /// Executes the OdfMediaManager operation.
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
            if (kvp.Key.StartsWith(PicturesEntryPrefix, StringComparison.Ordinal))
            {
                try
                {
                    using var stream = _package.GetEntryStream(kvp.Key);
                    using var ms = new MemoryStream();
                    OdfBoundedStreamReader.CopyTo(stream, ms, _package.LoadOptions.MaxEntrySize, "Err_OdfMediaManager_ExistingMediaEntryTooLarge");
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
    /// Executes the AddImage operation.
    /// 插入圖片二進位資料。若圖片內容已存在，則會自動重用現有路徑，實現自動重複資料刪除。
    /// </summary>
    /// <param name="imageBytes">圖片的二進位內容</param>
    /// <param name="preferredName">偏好的檔名（若重複資料刪除未命中且未給定時會自動產生）</param>
    /// <returns>傳回該圖片在 ODF 封裝中的相對路徑（例如 "Pictures/image_hash.png" ）</returns>
    public string AddImage(byte[] imageBytes, string? preferredName = null)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfMediaManager_ImageCannotBeEmpty"), nameof(imageBytes));
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

        // 3. 解析專案路徑
        string entryPath;
        if (!string.IsNullOrWhiteSpace(preferredName))
        {
            string sanitizedName = Path.GetFileName(preferredName);
            if (!sanitizedName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                sanitizedName += extension;
            }
            entryPath = OdfPackage.SanitizeEntryName($"{PicturesEntryPrefix}{sanitizedName}");
        }
        else
        {
            // 預設後備路徑
            _fallbackImageCounter++;
            entryPath = OdfPackage.SanitizeEntryName($"{PicturesEntryPrefix}image_{_fallbackImageCounter}_{hash.Substring(0, 8)}{extension}");
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
    /// 依序比對的圖片格式簽章表，每一列以判斷式描述辨識條件，對應其 MIME 類型與副檔名。
    /// </summary>
    private static readonly (Func<byte[], bool> Matches, string MediaType, string Extension)[] ImageFormatSignatures =
    [
        (b => b.Length >= 8 &&
              b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 &&
              b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A,
            "image/png", ".png"),
        (b => b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF,
            "image/jpeg", ".jpg"),
        (b => b.Length >= 4 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x38,
            "image/gif", ".gif"),
        // WebP 格式 (RIFF....WEBP)
        (b => b.Length >= 12 &&
              b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && // RIFF
              b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50, // WEBP
            "image/webp", ".webp"),
        // BMP 格式 (BM)
        (b => b.Length >= 2 && b[0] == 0x42 && b[1] == 0x4D,
            "image/bmp", ".bmp"),
        // TIFF 格式 (II* / MM*)
        (b => b.Length >= 4 &&
              ((b[0] == 0x49 && b[1] == 0x49 && b[2] == 0x2A && b[3] == 0x00) || // Little Endian
               (b[0] == 0x4D && b[1] == 0x4D && b[2] == 0x00 && b[3] == 0x2A)),  // Big Endian
            "image/tiff", ".tiff"),
        // EMF 格式（開頭 0x01 0x00 0x00 0x00，且偏移 40 處為 " EMF"）
        (b => b.Length >= 44 &&
              b[0] == 0x01 && b[1] == 0x00 && b[2] == 0x00 && b[3] == 0x00 &&
              b[40] == 0x20 && b[41] == 0x45 && b[42] == 0x4D && b[43] == 0x46,
            "image/x-emf", ".emf"),
        // WMF 格式（傳統簽章）
        (b => b.Length >= 4 && b[0] == 0xD7 && b[1] == 0xCD && b[2] == 0xC6 && b[3] == 0x9A,
            "image/x-wmf", ".wmf"),
        // WMF 格式（替代表頭變體）
        (b => b.Length >= 10 &&
              ((b[0] == 0x01 && b[1] == 0x00) || (b[0] == 0x09 && b[1] == 0x00)) &&
              b[2] == 0x00 && b[3] == 0x00,
            "image/x-wmf", ".wmf"),
        // SVG 簡單檢查（文字內容，非固定位移 magic bytes）
        (IsSvg, "image/svg+xml", ".svg"),
    ];

    /// <summary>
    /// Executes the DetectImageFormat operation.
    /// 根據檔案的幻數（Magic Bytes）偵測圖片格式。
    /// </summary>
    /// <param name="bytes">圖片的二進位內容</param>
    /// <param name="mimeType">輸出的 MIME 類型</param>
    /// <param name="extension">輸出的副檔名，包含前導句點</param>
    public static void DetectImageFormat(byte[] bytes, out string mimeType, out string extension)
    {
        foreach (var signature in ImageFormatSignatures)
        {
            if (signature.Matches(bytes))
            {
                mimeType = signature.MediaType;
                extension = signature.Extension;
                return;
            }
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

