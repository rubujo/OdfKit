namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents read-only summary information for a spreadsheet DDE link.
/// 表示試算表 DDE 連結的唯讀摘要資訊。
/// </summary>
/// <param name="application">The DDE application or service name. / DDE 應用程式或服務名稱。</param>
/// <param name="topic">The DDE topic, commonly a source file or document. / DDE 主題，通常為來源檔案或文件。</param>
/// <param name="item">The DDE item, such as a source range or object name. / DDE 項目，例如來源範圍或物件名稱。</param>
/// <param name="name">The optional ODF DDE source name. / 選用的 ODF DDE 來源名稱。</param>
/// <param name="conversionMode">The optional ODF conversion mode. / 選用的 ODF 轉換模式。</param>
/// <param name="automaticUpdate">The optional automatic-update setting, or <see langword="null"/> when unspecified. / 選用的自動更新設定；未指定時為 <see langword="null"/>。</param>
/// <param name="hasCachedTable">Whether the link contains the table that stores data from the last connection. / 連結是否包含儲存上次連線資料的表格。</param>
/// <param name="cachedTableName">The optional name of the cached table. / 快取表格的選用名稱。</param>
public sealed class OdfDdeLinkInfo(
    string? application,
    string? topic,
    string? item,
    string? name,
    string? conversionMode,
    bool? automaticUpdate,
    bool hasCachedTable,
    string? cachedTableName)
{
    /// <summary>
    /// Gets the DDE application or service name.
    /// 取得 DDE 應用程式或服務名稱。
    /// </summary>
    public string? Application { get; } = application;

    /// <summary>
    /// Gets the DDE topic, commonly a source file or document.
    /// 取得 DDE 主題，通常為來源檔案或文件。
    /// </summary>
    public string? Topic { get; } = topic;

    /// <summary>
    /// Gets the DDE item, such as a source range or object name.
    /// 取得 DDE 項目，例如來源範圍或物件名稱。
    /// </summary>
    public string? Item { get; } = item;

    /// <summary>
    /// Gets the optional ODF DDE source name.
    /// 取得選用的 ODF DDE 來源名稱。
    /// </summary>
    public string? Name { get; } = name;

    /// <summary>
    /// Gets the optional ODF conversion mode.
    /// 取得選用的 ODF 轉換模式。
    /// </summary>
    public string? ConversionMode { get; } = conversionMode;

    /// <summary>
    /// Gets the optional automatic-update setting, or <see langword="null"/> when unspecified.
    /// 取得選用的自動更新設定；未指定時為 <see langword="null"/>。
    /// </summary>
    public bool? AutomaticUpdate { get; } = automaticUpdate;

    /// <summary>
    /// Gets whether the link contains the table that stores data from the last connection.
    /// 取得連結是否包含儲存上次連線資料的表格。
    /// </summary>
    public bool HasCachedTable { get; } = hasCachedTable;

    /// <summary>
    /// Gets the optional name of the cached table.
    /// 取得快取表格的選用名稱。
    /// </summary>
    public string? CachedTableName { get; } = cachedTableName;
}
