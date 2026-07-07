#pragma warning restore CS1591

using System;

namespace OdfKit.Core;

/// <summary>
/// Provides the OdfManifestFileEntryIssue API.
/// 表示資訊清單（Manifest）檔案專案中的合規性問題。
/// </summary>
public sealed class OdfManifestFileEntryIssue
{
    /// <summary>
    /// Gets the FullPath value.
    /// 取得或設定專案的完整路徑。
    /// </summary>
    public string? FullPath { get; set; }

    /// <summary>
    /// Gets a value indicating the MissingFullPath state.
    /// 取得或設定一個值，指出是否遺失完整路徑屬性。
    /// </summary>
    public bool MissingFullPath { get; set; }

    /// <summary>
    /// Gets a value indicating the MissingMediaType state.
    /// 取得或設定一個值，指出是否遺失媒體類型屬性。
    /// </summary>
    public bool MissingMediaType { get; set; }

    /// <summary>
    /// Gets a value indicating the InvalidFullPath state.
    /// 取得或設定一個值，指出完整路徑是否無效。
    /// </summary>
    public bool InvalidFullPath { get; set; }
}

/// <summary>
/// Provides the OdfManifestRootInfo API.
/// 表示資訊清單根專案的資訊。
/// </summary>
/// <remarks>
/// 建立新的 <see cref="OdfManifestRootInfo"/> 類別執行個體。
/// </remarks>
/// <param name="namespaceUri">命名空間 URI</param>
/// <param name="localName">區域名稱</param>
/// <param name="version">版本字串</param>
public sealed class OdfManifestRootInfo(string namespaceUri, string localName, string? version)
{
    /// <summary>
    /// Gets the NamespaceUri value.
    /// 取得命名空間 URI。
    /// </summary>
    public string NamespaceUri { get; } = namespaceUri;

    /// <summary>
    /// Gets the LocalName value.
    /// 取得區域名稱。
    /// </summary>
    public string LocalName { get; } = localName;

    /// <summary>
    /// Gets the Version value.
    /// 取得版本字串。
    /// </summary>
    public string? Version { get; } = version;
}

