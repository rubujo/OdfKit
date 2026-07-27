using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// Provides embedded-object package management operations.
/// 提供內嵌物件封裝管理作業。
/// </summary>
public sealed partial class OdfPackage
{
    /// <summary>
    /// Gets descriptors for embedded ODF subdocuments.
    /// 取得內嵌 ODF 子文件的描述資訊。
    /// </summary>
    /// <returns>The embedded document descriptors. / 內嵌文件描述資訊集合。</returns>
    public IReadOnlyList<OdfEmbeddedObjectInfo> GetEmbeddedObjectInfos()
    {
        List<OdfEmbeddedObjectInfo> result = [];
        foreach (KeyValuePair<string, string> item in _manifest.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            string manifestPath = item.Key;
            string mediaType = item.Value;
            if (manifestPath == "/" ||
                !mediaType.StartsWith("application/vnd.oasis.opendocument.", StringComparison.Ordinal))
            {
                continue;
            }

            string objectPath = manifestPath.TrimEnd('/');
            string prefix = objectPath + "/";
            string[] entries = _entries.Keys
                .Where(path => path.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (entries.Length == 0)
            {
                continue;
            }

            result.Add(new OdfEmbeddedObjectInfo(this, objectPath, mediaType, entries));
        }

        return result;
    }

    /// <summary>
    /// Adds an ODF document as an embedded subdocument.
    /// 將 ODF 文件新增為內嵌子文件。
    /// </summary>
    /// <param name="path">The destination object directory path. / 目標物件目錄路徑。</param>
    /// <param name="document">The document to embed. / 要內嵌的文件。</param>
    /// <returns>The embedded document descriptor. / 內嵌文件描述資訊。</returns>
    public OdfEmbeddedObjectInfo AddEmbeddedDocument(string path, OdfDocument document)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(document, nameof(document));

        string objectPath = SanitizeEntryName(path).TrimEnd('/');
        if (string.IsNullOrEmpty(objectPath))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfPackage_EntryNameEmpty"), nameof(path));
        }

        string prefix = objectPath + "/";
        if (_entries.Keys.Any(entry => entry.StartsWith(prefix, StringComparison.Ordinal)) ||
            _manifest.ContainsKey(prefix))
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfPackage_EntryAlreadyExists", objectPath));
        }

        using var serializedDocument = new MemoryStream(document.SaveToBytes(), writable: false);
        using OdfPackage source = Open(serializedDocument);
        foreach (string sourcePath in source._entries.Keys.OrderBy(item => item, StringComparer.Ordinal))
        {
            if (sourcePath == "mimetype" || sourcePath.StartsWith("META-INF/", StringComparison.Ordinal))
            {
                continue;
            }

            string? mediaType = source._manifest.TryGetValue(sourcePath, out string? declaredMediaType)
                ? declaredMediaType
                : null;
            WriteEntry(prefix + sourcePath, source.ReadEntry(sourcePath), mediaType);
        }

        _manifest[prefix] = source.MimeType ?? "application/vnd.oasis.opendocument.text";
        return GetEmbeddedObjectInfos().Single(item => item.Path == objectPath);
    }

    /// <summary>
    /// Replaces an embedded ODF subdocument.
    /// 取代內嵌 ODF 子文件。
    /// </summary>
    /// <param name="path">The object directory path. / 物件目錄路徑。</param>
    /// <param name="document">The replacement document. / 用來取代的文件。</param>
    /// <returns>The replacement embedded document descriptor. / 取代後的內嵌文件描述資訊。</returns>
    public OdfEmbeddedObjectInfo ReplaceEmbeddedDocument(string path, OdfDocument document)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(document, nameof(document));

        RemoveEmbeddedObject(path);
        return AddEmbeddedDocument(path, document);
    }

    /// <summary>
    /// Removes an embedded object directory and all of its entries.
    /// 移除內嵌物件目錄及其所有項目。
    /// </summary>
    /// <param name="path">The object directory path. / 物件目錄路徑。</param>
    /// <returns><see langword="true"/> if package content was removed; otherwise, <see langword="false"/>. / 若已移除封裝內容則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveEmbeddedObject(string path)
    {
        string objectPath = SanitizeEntryName(path).TrimEnd('/');
        string prefix = objectPath + "/";
        string[] entries = _entries.Keys
            .Where(entry => entry.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
        bool removed = false;
        foreach (string entry in entries)
        {
            removed |= RemoveEntry(entry);
        }

        foreach (string manifestPath in _manifest.Keys
            .Where(entry => entry == prefix || entry.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray())
        {
            removed |= _manifest.Remove(manifestPath);
        }

        return removed;
    }
}
