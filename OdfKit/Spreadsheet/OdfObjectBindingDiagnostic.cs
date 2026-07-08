using OdfKit.Compliance;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Describes a non-fatal object binding diagnostic.
/// 描述非致命的物件繫結診斷資訊。
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="OdfObjectBindingDiagnostic"/> class.
/// 初始化 <see cref="OdfObjectBindingDiagnostic"/> 類別的新執行個體。
/// </remarks>
/// <param name="severity">The diagnostic severity. / 診斷嚴重性。</param>
/// <param name="messageKey">The localization message key. / 在地化訊息鍵值。</param>
/// <param name="row">The zero-based row index, or -1 when not row-specific. / 0 基準列索引；不適用特定列時為 -1。</param>
/// <param name="column">The zero-based column index, or -1 when not column-specific. / 0 基準欄索引；不適用特定欄時為 -1。</param>
/// <param name="propertyName">The object property name. / 物件屬性名稱。</param>
/// <param name="rawValue">The raw cell value. / 原始儲存格值。</param>
/// <param name="code">The stable diagnostic code. / 穩定診斷代碼。</param>
/// <param name="cellAddress">The cell address, if available. / 可用時的儲存格位址。</param>
/// <param name="expectedType">The expected target type. / 預期目標型別。</param>
/// <param name="actualType">The actual source type. / 實際來源型別。</param>
public sealed class OdfObjectBindingDiagnostic(
    OdfIssueSeverity severity,
    string messageKey,
    int row,
    int column,
    string propertyName,
    string? rawValue,
    string? code = null,
    string? cellAddress = null,
    string? expectedType = null,
    string? actualType = null)
{
    /// <summary>
    /// Gets the diagnostic severity.
    /// 取得診斷嚴重性。
    /// </summary>
    public OdfIssueSeverity Severity { get; } = severity;

    /// <summary>
    /// Gets the localization message key.
    /// 取得在地化訊息鍵值。
    /// </summary>
    public string MessageKey { get; } = messageKey;

    /// <summary>
    /// Gets the zero-based row index, or -1 when not row-specific.
    /// 取得 0 基準列索引；不適用特定列時為 -1。
    /// </summary>
    public int Row { get; } = row;

    /// <summary>
    /// Gets the zero-based column index, or -1 when not column-specific.
    /// 取得 0 基準欄索引；不適用特定欄時為 -1。
    /// </summary>
    public int Column { get; } = column;

    /// <summary>
    /// Gets the object property name.
    /// 取得物件屬性名稱。
    /// </summary>
    public string PropertyName { get; } = propertyName;

    /// <summary>
    /// Gets the raw cell value.
    /// 取得原始儲存格值。
    /// </summary>
    public string? RawValue { get; } = rawValue;

    /// <summary>
    /// Gets the stable diagnostic code.
    /// 取得穩定診斷代碼。
    /// </summary>
    public string? Code { get; } = code;

    /// <summary>
    /// Gets the cell address, if available.
    /// 取得可用時的儲存格位址。
    /// </summary>
    public string? CellAddress { get; } = cellAddress;

    /// <summary>
    /// Gets the expected target type.
    /// 取得預期目標型別。
    /// </summary>
    public string? ExpectedType { get; } = expectedType;

    /// <summary>
    /// Gets the actual source type.
    /// 取得實際來源型別。
    /// </summary>
    public string? ActualType { get; } = actualType;
}
