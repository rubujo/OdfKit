using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.DOM;

#region Manifest Wrappers


/// <summary>
/// 表示 ODF 資訊清單中的 manifest:manifest 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class ManifestManifestElement(string? prefix = null) : OdfElement("manifest", OdfNamespaces.Manifest, prefix);

/// <summary>
/// 表示 ODF 資訊清單中的 manifest:file-entry 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class ManifestFileEntryElement(string? prefix = null) : OdfElement("file-entry", OdfNamespaces.Manifest, prefix)
{
    /// <summary>
    /// 取得或設定檔案專案在套件中的完整路徑。
    /// </summary>
    public string? FullPath
    {
        get => GetAttributeValue("full-path", OdfNamespaces.Manifest, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("full-path", OdfNamespaces.Manifest);
            else
                SetAttributeValue("full-path", OdfNamespaces.Manifest, value, OdfNamespaces.GetPrefix(OdfNamespaces.Manifest), GetDocumentVersion());
        }
    }

    /// <summary>
    /// 取得或設定檔案專案的媒體類型。
    /// </summary>
    public string? MediaType
    {
        get => GetAttributeValue("media-type", OdfNamespaces.Manifest, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("media-type", OdfNamespaces.Manifest);
            else
                SetAttributeValue("media-type", OdfNamespaces.Manifest, value, OdfNamespaces.GetPrefix(OdfNamespaces.Manifest), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 資訊清單中的 manifest:encryption-data 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class ManifestEncryptionDataElement(string? prefix = null) : OdfElement("encryption-data", OdfNamespaces.Manifest, prefix);


#endregion
