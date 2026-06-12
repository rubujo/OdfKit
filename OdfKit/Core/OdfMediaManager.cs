#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System.Security.Cryptography;
using System.Text;

namespace OdfKit.Core
{
    public class OdfMediaManager
    {
        private readonly OdfPackage _package;
        // Dictionary mapping SHA-256 image hashes to their ZIP entry paths (e.g. "Pictures/image_hash.png")
        private readonly Dictionary<string, string> _imageHashRegistry = new(StringComparer.Ordinal);
        private int _fallbackImageCounter = 0;

        public OdfMediaManager(OdfPackage package)
        {
            _package = package;
            ScanExistingMedia();
        }

        private void ScanExistingMedia()
        {
            // Scan manifest to find existing media in Pictures/
            foreach (var kvp in _package.Manifest)
            {
                if (kvp.Key.StartsWith("Pictures/"))
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
                        OdfKitDiagnostics.Warn($"Failed to scan existing media entry '{kvp.Key}': {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 插入圖片二進位資料。若圖片內容已存在，則會自動重用現有路徑，實現自動去重。
        /// </summary>
        /// <param name="imageBytes">圖片的二進位內容</param>
        /// <param name="preferredName">偏好的檔名（若去重未命中且未給定時會自動產出）</param>
        /// <returns>回傳該圖片在 ODF 封裝中的相對路徑（如 "Pictures/image_hash.png"）</returns>
        public string AddImage(byte[] imageBytes, string? preferredName = null)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                throw new ArgumentException("Image data cannot be null or empty.", nameof(imageBytes));
            }

            // 1. Calculate SHA-256 hash for deduplication
            string hash = ComputeSha256(imageBytes);
            if (_imageHashRegistry.TryGetValue(hash, out string? existingPath))
            {
                OdfKitDiagnostics.Info($"Reused existing image entry: {existingPath}");
                return existingPath;
            }

            // 2. Detect image format from magic bytes
            DetectImageFormat(imageBytes, out string mimeType, out string extension);

            // 3. Resolve entry path
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
                // Default fallback: Pictures/1000000000000001.png etc.
                _fallbackImageCounter++;
                entryPath = OdfPackage.SanitizeEntryName($"Pictures/image_{_fallbackImageCounter}_{hash.Substring(0, 8)}{extension}");
            }

            // Resolve name collision
            int collisionCounter = 0;
            string finalPath = entryPath;
            while (_package.HasEntry(finalPath))
            {
                collisionCounter++;
                string dir = Path.GetDirectoryName(entryPath)?.Replace('\\', '/') ?? "Pictures";
                string nameWithoutExt = Path.GetFileNameWithoutExtension(entryPath);
                finalPath = OdfPackage.SanitizeEntryName($"{dir}/{nameWithoutExt}_{collisionCounter}{extension}");
            }

            // 4. Write to package
            _package.WriteEntry(finalPath, imageBytes, mimeType);
            _imageHashRegistry[hash] = finalPath;

            OdfKitDiagnostics.Info($"Inserted new image entry: {finalPath} ({mimeType})");
            return finalPath;
        }

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

            // WebP format (RIFF....WEBP)
            if (bytes.Length >= 12 && 
                bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 && // RIFF
                bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) // WEBP
            {
                mimeType = "image/webp";
                extension = ".webp";
                return;
            }

            // BMP format (BM)
            if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D)
            {
                mimeType = "image/bmp";
                extension = ".bmp";
                return;
            }

            // TIFF format (II* / MM*)
            if (bytes.Length >= 4 &&
                ((bytes[0] == 0x49 && bytes[1] == 0x49 && bytes[2] == 0x2A && bytes[3] == 0x00) || // Little Endian
                 (bytes[0] == 0x4D && bytes[1] == 0x4D && bytes[2] == 0x00 && bytes[3] == 0x2A)))  // Big Endian)
            {
                mimeType = "image/tiff";
                extension = ".tiff";
                return;
            }

            // EMF format (0x01 0x00 0x00 0x00 at start, and " EMF" at offset 40)
            if (bytes.Length >= 44 &&
                bytes[0] == 0x01 && bytes[1] == 0x00 && bytes[2] == 0x00 && bytes[3] == 0x00 &&
                bytes[40] == 0x20 && bytes[41] == 0x45 && bytes[42] == 0x4D && bytes[43] == 0x46)
            {
                mimeType = "image/x-emf";
                extension = ".emf";
                return;
            }

            // WMF format (placeable 0xD7 0xCD 0xC6 0x9A, or standard 0x01 0x00 / 0x09 0x00)
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

            // SVG simple check
            if (IsSvg(bytes))
            {
                mimeType = "image/svg+xml";
                extension = ".svg";
                return;
            }

            // Default fallback
            mimeType = "application/octet-stream";
            extension = ".bin";
        }

        private static bool IsSvg(byte[] bytes)
        {
            // Read first 512 bytes as string to scan for svg signature
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
}
