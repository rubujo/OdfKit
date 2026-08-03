using System.IO;
using System;
using System.Collections.Generic;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.DOM;
/// <summary>
/// Provides the OdfNode API.
/// 提供 OdfNode API。
/// </summary>

public partial class OdfNode
{
    #region Clone & Import Node


    // 複製遞迴深度計數器（執行緒個別）：防止透過公開 DOM API 疊出的極深巢狀樹
    // 在 CloneNode/ImportNode 遞迴時引發 StackOverflowException，與 OdfXmlReader.MaxElementDepth
    // 對剖析路徑的深度防護保持一致。OdfElement.CloneNode 覆寫亦共用此計數器。
    /// <summary>
    /// Provides the member member.
    /// 提供 member 成員。
    /// </summary>
    [ThreadStatic]
    private protected static int CloneRecursionDepth;

    /// <summary>
    /// Clones node.
    /// 複製當前節點。
    /// </summary>
    /// <param name="deep">是否進行深層複製（遞迴複製子節點）</param>
    /// <returns>複製的新節點</returns>
    public virtual OdfNode CloneNode(bool deep)
    {
        var clone = new OdfNode(NodeType, LocalName, NamespaceUri, Prefix)
        {
            _value = _value
        };

        foreach (var attr in Attributes)
        {
            clone.Attributes[attr.Key] = attr.Value;
        }

        foreach (var attrPrefix in _attributePrefixes)
        {
            clone._attributePrefixes[attrPrefix.Key] = attrPrefix.Value;
        }

        if (deep && TryCopyLazyXmlStateTo(clone))
        {
            return clone;
        }

        if (deep)
        {
            if (++CloneRecursionDepth > OdfXmlReader.MaxElementDepth)
            {
                CloneRecursionDepth--;
                throw new System.Security.SecurityException(
                    OdfLocalizer.GetMessage("Err_OdfXmlReader_XmlElementNestingDepth", CloneRecursionDepth, OdfXmlReader.MaxElementDepth));
            }

            try
            {
                foreach (var child in Children)
                {
                    clone.AppendChild(child.CloneNode(true));
                }
            }
            finally
            {
                CloneRecursionDepth--;
            }
        }

        return clone;
    }

    internal bool TryCopyLazyXmlStateTo(OdfNode clone)
    {
        if (!_isLazy || Children.LoadedCount != 0)
        {
            return false;
        }

        clone._isLazy = true;
        clone._lazyXmlMemory = _lazyXmlMemory;
        clone._lazyXmlPtr = _lazyXmlPtr;
        clone._lazyXmlLen = _lazyXmlLen;
        clone._xmlByteRange = _xmlByteRange;
        clone._lazyMaxXmlCharactersInDocument = _lazyMaxXmlCharactersInDocument;
        clone._lazyStrictXmlParsing = _lazyStrictXmlParsing;
        clone._sourceNamespacePrefixes = _sourceNamespacePrefixes is null
            ? null
            : new Dictionary<string, string>(_sourceNamespacePrefixes, StringComparer.Ordinal);
        return true;
    }

    /// <summary>
    /// Imports node.
    /// 將一個節點從來源 <see cref="OdfPackage"/> 匯入至目的 <see cref="OdfPackage"/>，自動複製並移轉其所屬的媒體檔案與樣式關聯。
    /// </summary>
    /// <param name="sourceNode">要匯入的來源節點</param>
    /// <param name="sourcePackage">來源套件</param>
    /// <param name="destPackage">目的套件</param>
    /// <returns>匯入後的新節點</returns>
    public static OdfNode ImportNode(OdfNode sourceNode, OdfPackage? sourcePackage, OdfPackage? destPackage)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(sourceNode, nameof(sourceNode));

        // 先深層複製節點結構
        OdfNode importedNode = sourceNode.CloneNode(true);

        // 如果在不同的套件之間遷移，則掃描並重寫媒體或圖片資源
        if (sourcePackage is not null && destPackage is not null && sourcePackage != destPackage)
        {
            MigrateMediaReferences(importedNode, sourcePackage, destPackage);
        }

        return importedNode;
    }

    internal static void MigrateMediaReferences(OdfNode node, OdfPackage sourcePackage, OdfPackage destPackage)
    {
        // 檢查節點中的 xlink:href 屬性
        var hrefKey = new OdfAttributeName("href", OdfNamespaces.XLink);
        if (node.Attributes.TryGetValue(hrefKey, out string? href) && href is not null)
        {
            // 媒體參考通常位於 zip 套件內的 OdfMediaManager.PicturesEntryPrefix 目錄下
            if (href.StartsWith(OdfMediaManager.PicturesEntryPrefix, StringComparison.Ordinal))
            {
                try
                {
                    using var stream = sourcePackage.GetEntryStream(href);
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    byte[] mediaBytes = ms.ToArray();

                    // 使用目的套件的共用 MediaManager 快取進行註冊，防止重複實例化與 Pictures 全掃描
                    string fileName = Path.GetFileName(href);
                    string newHref = destPackage.MediaManager.AddImage(mediaBytes, fileName);

                    // 更新複製節點中的參考
                    node.Attributes[hrefKey] = newHref;
                }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Warn($"Failed to migrate media reference '{href}' during node import: {ex.Message}");
                }
            }
            else
            {
                string normHref = href.TrimStart('.', '/').TrimEnd('/');
                string folderPrefix = normHref + "/";
                var entriesToCopy = sourcePackage.GetEntries()
                                                  .Where(e => e.Path.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
                                                  .ToList();

                if (entriesToCopy.Count > 0)
                {
                    string originalPrefix = normHref.StartsWith("Object", StringComparison.OrdinalIgnoreCase) ? "Object" : "Formula";
                    // 使用完整 32 位元十六進位 GUID（而非截斷至 8 碼），避免高併發匯入下的檔名碰撞風險。
                    string newHref = $"{originalPrefix}_{Guid.NewGuid():N}";

                    var writtenPaths = new List<string>();
                    var failedPaths = new List<string>();

                    foreach (var entryInfo in entriesToCopy)
                    {
                        string srcPath = entryInfo.Path;
                        string relativePath = srcPath.Substring(folderPrefix.Length);
                        string destPath = $"{newHref}/{relativePath}";

                        try
                        {
                            byte[] bytes = sourcePackage.ReadEntry(srcPath);
                            string mediaType = sourcePackage.Manifest.TryGetValue(srcPath, out var m) ? m : "text/xml";
                            if (relativePath == "mimetype")
                            {
                                mediaType = Encoding.UTF8.GetString(bytes).Trim();
                            }
                            destPackage.WriteEntry(destPath, bytes, mediaType);
                            writtenPaths.Add(destPath);
                        }
                        catch (Exception ex)
                        {
                            OdfKitDiagnostics.Warn($"Failed to migrate embedded entry '{srcPath}' during node import: {ex.Message}");
                            failedPaths.Add(srcPath);
                        }
                    }

                    if (failedPaths.Count > 0)
                    {
                        // 部分項目搬移失敗：移除已寫入的殘缺項目，並保留原始 href，避免產生
                        // 引用不完整內嵌物件資料夾的損毀參照。
                        foreach (string writtenPath in writtenPaths)
                        {
                            destPackage.RemoveEntry(writtenPath);
                        }

                        OdfKitDiagnostics.Warn($"Embedded object migration for '{href}' aborted due to {failedPaths.Count} failed entr(y/ies); original reference was left unchanged.");
                    }
                    else
                    {
                        node.Attributes[hrefKey] = newHref;
                        destPackage.SaveManifestToEntries();
                    }
                }
            }
        }

        // 遞迴子節點
        foreach (var child in node.Children)
        {
            MigrateMediaReferences(child, sourcePackage, destPackage);
        }
    }


    #endregion
}
