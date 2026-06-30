namespace OdfKit.Collaboration;

/// <summary>
/// Defines safety limits for ODT JSON operation parsing and replay.
/// 定義 ODT JSON operation 剖析與重播的安全限制。
/// </summary>
public sealed class OdtOperationSafetyOptions
{
    /// <summary>
    /// Gets or sets the maximum input JSON length in characters.
    /// 取得或設定輸入 JSON 的最大字元長度。
    /// </summary>
    public int MaxJsonLength { get; set; } = 8 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum JSON nesting depth.
    /// 取得或設定 JSON 巢狀深度上限。
    /// </summary>
    public int MaxJsonDepth { get; set; } = 64;

    /// <summary>
    /// Gets or sets the maximum number of operations in a log.
    /// 取得或設定 operation log 可包含的 operation 數量上限。
    /// </summary>
    public int MaxOperationCount { get; set; } = 100_000;

    /// <summary>
    /// Gets or sets the maximum text payload length per operation.
    /// 取得或設定每個 operation 的文字內容長度上限。
    /// </summary>
    public int MaxTextLength { get; set; } = 1 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum raw attribute payload length per operation.
    /// 取得或設定每個 operation 的原始屬性 payload 長度上限。
    /// </summary>
    public int MaxAttributesLength { get; set; } = 256 * 1024;

    /// <summary>
    /// Gets or sets the maximum absolute position component value.
    /// 取得或設定位置元件的最大絕對值。
    /// </summary>
    public int MaxPositionComponent { get; set; } = 10_000_000;

    /// <summary>
    /// Gets or sets the maximum table row count created by collaboration replay.
    /// 取得或設定協作重播可建立的表格列數上限。
    /// </summary>
    public int MaxTableRows { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the maximum table column count created by collaboration replay.
    /// 取得或設定協作重播可建立的表格欄數上限。
    /// </summary>
    public int MaxTableColumns { get; set; } = 1_000;

    /// <summary>
    /// Gets or sets the maximum total cell count created by collaboration replay.
    /// 取得或設定協作重播可建立的表格總儲存格數上限。
    /// </summary>
    public int MaxTableCells { get; set; } = 200_000;

    /// <summary>
    /// Gets or sets the maximum list nesting level accepted during replay.
    /// 取得或設定重播時接受的清單巢狀階層上限。
    /// </summary>
    public int MaxListLevel { get; set; } = 16;

    /// <summary>
    /// Gets or sets the maximum selection payload length per operation.
    /// 取得或設定每個 operation 的 selection payload 長度上限。
    /// </summary>
    public int MaxSelectionLength { get; set; } = 128 * 1024;

    /// <summary>
    /// Gets or sets the maximum drawing attribute payload length per operation.
    /// 取得或設定每個 drawing operation 的屬性 payload 長度上限。
    /// </summary>
    public int MaxDrawingAttributesLength { get; set; } = 64 * 1024;

    /// <summary>
    /// Gets or sets the maximum base64-like string payload length accepted in attrs.
    /// 取得或設定 attrs 中允許的類 base64 字串 payload 長度上限。
    /// </summary>
    public int MaxBinaryLikeAttributeLength { get; set; } = 16 * 1024;

    /// <summary>
    /// Gets or sets whether external URI-like attributes are rejected during parsing.
    /// 取得或設定剖析時是否拒絕外部 URI 類屬性。
    /// </summary>
    public bool RejectExternalResourceAttributes { get; set; } = true;

    /// <summary>
    /// Gets or sets whether script and event handler attributes are rejected during parsing.
    /// 取得或設定剖析時是否拒絕 script 與事件處理器屬性。
    /// </summary>
    public bool RejectScriptAndEventAttributes { get; set; } = true;
}
