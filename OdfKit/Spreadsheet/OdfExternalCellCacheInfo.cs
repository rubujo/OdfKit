namespace OdfKit.Spreadsheet;

/// <summary>
/// Describes a cached cell value for an external spreadsheet reference.
/// 描述試算表外部參照的快取儲存格值。
/// </summary>
public sealed class OdfExternalCellCacheInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdfExternalCellCacheInfo"/> class.
    /// 初始化 <see cref="OdfExternalCellCacheInfo"/> 類別的新執行個體。
    /// </summary>
    /// <param name="documentId">The external document identifier. / 外部文件識別碼。</param>
    /// <param name="sheetName">The external worksheet name. / 外部工作表名稱。</param>
    /// <param name="address">The external cell address. / 外部儲存格位址。</param>
    /// <param name="value">The cached value. / 快取值。</param>
    public OdfExternalCellCacheInfo(string documentId, string sheetName, OdfCellAddress address, object? value)
    {
        DocumentId = documentId;
        SheetName = sheetName;
        Address = address;
        Value = value;
    }

    /// <summary>
    /// Gets the external document identifier.
    /// 取得外部文件識別碼。
    /// </summary>
    public string DocumentId { get; }

    /// <summary>
    /// Gets the external worksheet name.
    /// 取得外部工作表名稱。
    /// </summary>
    public string SheetName { get; }

    /// <summary>
    /// Gets the external cell address.
    /// 取得外部儲存格位址。
    /// </summary>
    public OdfCellAddress Address { get; }

    /// <summary>
    /// Gets the cached value.
    /// 取得快取值。
    /// </summary>
    public object? Value { get; }
}
