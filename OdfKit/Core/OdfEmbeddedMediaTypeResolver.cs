using System;
using System.IO;
using System.Security;
using System.Text;
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// 以內嵌目錄宣告或有界 mimetype 讀取判定文件種類。
/// </summary>
internal static class OdfEmbeddedMediaTypeResolver
{
    internal static OdfDocumentKind Resolve(OdfPackage package, string subPath)
    {
        string directory = global::OdfKit.Internal.OdfStringHelper.EndsWith(subPath, '/') ? subPath : subPath + "/";
        if (package.Manifest.TryGetValue(directory, out string? mediaType) && !string.IsNullOrEmpty(mediaType))
            return OdfDocumentKindDetector.FromMimeType(mediaType);
        string path = directory + "mimetype";
        if (!package.HasEntry(path))
            return OdfDocumentKind.Unknown;
        try
        {
            using Stream stream = package.GetEntryStream(path);
            // 已知 ODF MIME 名稱皆短於此界限；不為種類查找解壓任意長度的未知資料。
            byte[] buffer = new byte[128];
            int count = 0;
            while (count < buffer.Length)
            {
                int read = stream.Read(buffer, count, buffer.Length - count);
                if (read == 0)
                    return OdfDocumentKindDetector.FromMimeType(Encoding.UTF8.GetString(buffer, 0, count));
                count += read;
            }
        }
        catch (IOException)
        {
            return OdfDocumentKind.Unknown;
        }
        catch (SecurityException)
        {
            return OdfDocumentKind.Unknown;
        }
        return OdfDocumentKind.Unknown;
    }
}
