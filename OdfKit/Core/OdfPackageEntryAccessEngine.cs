using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using OdfKit.Compliance;
namespace OdfKit.Core;

/// <summary>
/// ODF 封裝專案讀寫引擎（內部協作者）。
/// </summary>
internal static class OdfPackageEntryAccessEngine
{
    private const string DocumentSignaturesPath = "META-INF/documentsignatures.xml";
    private const string ManifestPath = "META-INF/manifest.xml";

    /// <summary>
    /// 檢查封裝中是否包含指定名稱的專案。
    /// </summary>
    internal static bool HasEntry(OdfPackage.OdfPackageEntryCollaborators ctx, string name)
    {
        return ctx.Entries.ContainsKey(OdfPackage.SanitizeEntryName(name));
    }

    /// <summary>
    /// 取得封裝中所有實體專案的路徑集合。
    /// </summary>
    internal static IEnumerable<OdfPackage.OdfPackageEntryInfo> GetEntries(OdfPackage.OdfPackageEntryCollaborators ctx)
    {
        return ctx.Entries.Keys.Select(k => new OdfPackage.OdfPackageEntryInfo(k));
    }

    /// <summary>
    /// 讀取指定路徑專案的完整內容位元組。
    /// </summary>
    internal static byte[] ReadEntry(OdfPackage.OdfPackageEntryCollaborators ctx, string path)
    {
        using var stream = GetEntryStream(ctx, path);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// 取得指定專案的唯讀資料流。
    /// </summary>
    internal static Stream GetEntryStream(OdfPackage.OdfPackageEntryCollaborators ctx, string name)
    {
        name = OdfPackage.SanitizeEntryName(name);

        if (ctx.Entries.TryGetValue(name, out var entry))
        {
            return entry.OpenReader();
        }

        throw new FileNotFoundException(OdfLocalizer.GetMessage("Err_OdfPackageEntryAccessEngine_EntryNotFound", name));
    }

    /// <summary>
    /// 將指定的位元組內容寫入或覆寫封裝中的專案。
    /// </summary>
    internal static void WriteEntry(OdfPackage.OdfPackageEntryCollaborators ctx, string name, byte[] content, string? mediaType)
    {
        name = OdfPackage.SanitizeEntryName(name);
        string resolvedMediaType = OdfPackageMediaTypeResolver.Resolve(name, mediaType);
        OdfPackageEntry entry = new(name, content);
        ctx.Entries[name] = entry;
        ctx.Manifest[name] = resolvedMediaType;

        if (name.EndsWith("/mimetype") && name.Length > 9)
        {
            string folder = name.Substring(0, name.Length - 8);
            string mimeText = Encoding.UTF8.GetString(content).Trim();
            ctx.Manifest[folder] = mimeText;
        }

        if (name != DocumentSignaturesPath && name != ManifestPath)
        {
            ctx.RemoveOutdatedSignatures();
        }
    }

    /// <summary>
    /// 將指定的資料流內容寫入或覆寫封裝中的專案。
    /// </summary>
    internal static void WriteEntry(OdfPackage.OdfPackageEntryCollaborators ctx, string name, Stream contentStream, string? mediaType)
    {
        name = OdfPackage.SanitizeEntryName(name);
        string resolvedMediaType = OdfPackageMediaTypeResolver.Resolve(name, mediaType);
        OdfPackageEntry entry = new(name, contentStream);
        ctx.Entries[name] = entry;
        ctx.Manifest[name] = resolvedMediaType;

        if (name.EndsWith("/mimetype") && name.Length > 9)
        {
            string folder = name.Substring(0, name.Length - 8);
            byte[] bytes;
            using (MemoryStream ms = new())
            {
                contentStream.CopyTo(ms);
                bytes = ms.ToArray();
            }
            entry.SetContent(bytes);
            string mimeText = Encoding.UTF8.GetString(bytes).Trim();
            ctx.Manifest[folder] = mimeText;
        }

        if (name != DocumentSignaturesPath && name != ManifestPath)
        {
            ctx.RemoveOutdatedSignatures();
        }
    }

    /// <summary>
    /// 從封裝中移除指定的專案。
    /// </summary>
    internal static void RemoveEntry(OdfPackage.OdfPackageEntryCollaborators ctx, string name)
    {
        name = OdfPackage.SanitizeEntryName(name);
        ctx.Entries.Remove(name);
        ctx.Manifest.Remove(name);

        if (name != DocumentSignaturesPath && name != ManifestPath)
        {
            ctx.RemoveOutdatedSignatures();
        }
    }

    /// <summary>
    /// 清理封裝中未被參照的圖片等媒體檔案。
    /// </summary>
    internal static void PruneUnusedMedia(OdfPackage.OdfPackageEntryCollaborators ctx, IEnumerable<string> referencedMediaPaths)
    {
        HashSet<string> referencedSet = new(StringComparer.Ordinal);
        foreach (var path in referencedMediaPaths)
        {
            referencedSet.Add(OdfPackage.SanitizeEntryName(path));
        }

        List<string> keysToRemove = [];
        foreach (var key in ctx.Entries.Keys)
        {
            if (key.StartsWith("Pictures/", StringComparison.OrdinalIgnoreCase))
            {
                if (!referencedSet.Contains(key))
                {
                    keysToRemove.Add(key);
                }
            }
        }

        foreach (var key in keysToRemove)
        {
            RemoveEntry(ctx, key);
            OdfKitDiagnostics.Info($"Pruned unused media entry: {key}");
        }
    }

    /// <summary>
    /// 設定 ODF 封裝的主要 MIME 媒體類型。
    /// </summary>
    internal static void SetMimeType(OdfPackage.OdfPackageEntryCollaborators ctx, string mimetype)
    {
        ctx.SetMimeTypeValue(mimetype);
        WriteEntry(ctx, "mimetype", Encoding.UTF8.GetBytes(mimetype), string.Empty);
        if (ctx.Entries.TryGetValue("mimetype", out var mimeEntry))
        {
            mimeEntry.IsCompressed = false;
        }
    }

    /// <summary>
    /// 取得此封裝中所內嵌的 ODF 物件資料夾路徑清單。
    /// </summary>
    internal static IEnumerable<string> GetEmbeddedObjects(OdfPackage.OdfPackageEntryCollaborators ctx)
    {
        List<string> list = [];
        foreach (var kvp in ctx.Manifest)
        {
            if (kvp.Key != "/" && kvp.Value.StartsWith("application/vnd.oasis.opendocument.", StringComparison.Ordinal))
            {
                list.Add(kvp.Key);
            }
        }
        return list;
    }

    /// <summary>
    /// 擷取內嵌物件的主要內容 XML 資料流。
    /// </summary>
    internal static Stream ExtractObjectStream(OdfPackage.OdfPackageEntryCollaborators ctx, string objectName)
    {
        string path = OdfPackage.SanitizeEntryName(objectName);
        return GetEntryStream(ctx, path + "/content.xml");
    }

    /// <summary>
    /// 寫入虛擬專案（載入管線用，不觸發簽章清除）。
    /// </summary>
    internal static void WriteVirtualEntry(OdfPackage.OdfPackageEntryCollaborators ctx, string name, byte[] content, string mediaType)
    {
        name = OdfPackage.SanitizeEntryName(name);
        ctx.Entries[name] = new OdfPackageEntry(name, content);
        ctx.Manifest[name] = mediaType;
        if (!ctx.EntryOrder.Contains(name))
            ctx.EntryOrder.Add(name);
    }

    /// <summary>
    /// 判斷指定路徑的專案是否已加密。
    /// </summary>
    internal static bool IsEntryEncrypted(OdfPackage.OdfPackageEntryCollaborators ctx, string name)
    {
        name = OdfPackage.SanitizeEntryName(name);
        return ctx.Entries.TryGetValue(name, out var entry) && entry.EncryptionInfo != null;
    }

    /// <summary>
    /// 取得指定專案的加密詳細資訊。
    /// </summary>
    internal static OdfEncryptionInfo? GetEntryEncryptionInfo(OdfPackage.OdfPackageEntryCollaborators ctx, string name)
    {
        name = OdfPackage.SanitizeEntryName(name);
        return ctx.Entries.TryGetValue(name, out var entry) ? entry.EncryptionInfo : null;
    }
}
