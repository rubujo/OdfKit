using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using OdfKit.DOM;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    #region Public API


    /// <summary>
    /// 檢查封裝中是否包含指定名稱的項目。
    /// </summary>
    /// <param name="name">項目的相對路徑名稱</param>
    /// <returns>若項目存在則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    public bool HasEntry(string name)
    {
        return _entries.ContainsKey(SanitizeEntryName(name));
    }

    /// <summary>
    /// 提供 ODF 封裝中實體項目的基本資訊。
    /// </summary>
    public class OdfPackageEntryInfo
    {
        /// <summary>
        /// 取得項目的相對路徑。
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// 初始化 <see cref="OdfPackageEntryInfo"/> 類別的新執行個體。
        /// </summary>
        /// <param name="path">項目的相對路徑</param>
        public OdfPackageEntryInfo(string path) => Path = path;
    }

    /// <summary>
    /// 取得封裝中所有實體項目的資訊集合。
    /// </summary>
    /// <returns>所有項目的資訊集合</returns>
    public IEnumerable<OdfPackageEntryInfo> GetEntries()
    {
        return _entries.Keys.Select(k => new OdfPackageEntryInfo(k));
    }

    /// <summary>
    /// 讀取指定路徑項目的完整內容位元組。
    /// </summary>
    /// <param name="path">項目的相對路徑名稱</param>
    /// <returns>項目的位元組陣列內容</returns>
    public byte[] ReadEntry(string path)
    {
        using var stream = GetEntryStream(path);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

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
    /// 取得指定項目的唯讀資料流。
    /// </summary>
    /// <param name="name">項目的相對路徑名稱</param>
    /// <returns>代表項目內容的資料流</returns>
    public Stream GetEntryStream(string name)
    {
        name = SanitizeEntryName(name);

        if (_entries.TryGetValue(name, out var entry))
        {
            return entry.OpenReader();
        }

        throw new FileNotFoundException($"Entry '{name}' not found in ODF package.");
    }

    /// <summary>
    /// 將指定的位元組內容寫入或覆寫封裝中的項目。
    /// </summary>
    /// <param name="name">項目的相對路徑名稱</param>
    /// <param name="content">要寫入的位元組內容</param>
    /// <param name="mediaType">項目的 MIME 媒體類型</param>
    public void WriteEntry(string name, byte[] content, string mediaType)
    {
        name = SanitizeEntryName(name);
        OdfPackageEntry entry = new(name, content);
        _entries[name] = entry;
        _manifest[name] = mediaType;

        if (name.EndsWith("/mimetype") && name.Length > 9)
        {
            string folder = name.Substring(0, name.Length - 8); // keeps the trailing slash
            string mimeText = Encoding.UTF8.GetString(content).Trim();
            _manifest[folder] = mimeText;
        }

        // Clear signature on edit, except when writing signature itself or manifest
        if (name != "META-INF/documentsignatures.xml" && name != "META-INF/manifest.xml")
        {
            RemoveOutdatedSignatures();
        }
    }

    /// <summary>
    /// 將指定的資料流內容寫入或覆寫封裝中的項目。
    /// </summary>
    /// <param name="name">項目的相對路徑名稱</param>
    /// <param name="contentStream">要寫入的內容來源資料流</param>
    /// <param name="mediaType">項目的 MIME 媒體類型</param>
    public void WriteEntry(string name, Stream contentStream, string mediaType)
    {
        name = SanitizeEntryName(name);
        OdfPackageEntry entry = new(name, contentStream);
        _entries[name] = entry;
        _manifest[name] = mediaType;

        if (name.EndsWith("/mimetype") && name.Length > 9)
        {
            string folder = name.Substring(0, name.Length - 8); // keeps the trailing slash
            byte[] bytes;
            using (MemoryStream ms = new())
            {
                contentStream.CopyTo(ms);
                bytes = ms.ToArray();
            }
            entry.SetContent(bytes);
            string mimeText = Encoding.UTF8.GetString(bytes).Trim();
            _manifest[folder] = mimeText;
        }

        if (name != "META-INF/documentsignatures.xml" && name != "META-INF/manifest.xml")
        {
            RemoveOutdatedSignatures();
        }
    }

    /// <summary>
    /// 從封裝中移除指定的項目。
    /// </summary>
    /// <param name="name">要移除的項目相對路徑名稱</param>
    public void RemoveEntry(string name)
    {
        name = SanitizeEntryName(name);
        _entries.Remove(name);
        _manifest.Remove(name);

        if (name != "META-INF/documentsignatures.xml" && name != "META-INF/manifest.xml")
        {
            RemoveOutdatedSignatures();
        }
    }

    /// <summary>
    /// 清理封裝中未被參照的圖片等媒體檔案。
    /// </summary>
    /// <param name="referencedMediaPaths">所有目前正被參照的媒體檔案路徑集合</param>
    public void PruneUnusedMedia(IEnumerable<string> referencedMediaPaths)
    {
        HashSet<string> referencedSet = new(StringComparer.Ordinal);
        foreach (var path in referencedMediaPaths)
        {
            referencedSet.Add(SanitizeEntryName(path));
        }

        List<string> keysToRemove = [];
        foreach (var key in _entries.Keys)
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
            RemoveEntry(key);
            OdfKitDiagnostics.Info($"Pruned unused media entry: {key}");
        }
    }

    /// <summary>
    /// 設定 ODF 封裝的主要 MIME 媒體類型。
    /// </summary>
    /// <param name="mimetype">媒體類型字串</param>
    public void SetMimeType(string mimetype)
    {
        _mimetype = mimetype;
        WriteEntry("mimetype", Encoding.UTF8.GetBytes(mimetype), string.Empty);
        // Mimetype itself does not compression
        if (_entries.TryGetValue("mimetype", out var mimeEntry))
        {
            mimeEntry.IsCompressed = false;
        }
    }


    #endregion
}
