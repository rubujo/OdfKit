using System.Collections.Generic;
using System.Linq;
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// Unifies non-fatal diagnostic information across OdfKit's report types into a single strongly
/// typed shape, replacing ad-hoc string collections such as warning or missing-name lists.
/// 將 OdfKit 各報告類別的非致命診斷資訊統一為單一強型別形狀，取代個別的警告或缺漏名稱等
/// 字串集合。
/// </summary>
/// <param name="code">A short machine-readable code identifying the diagnostic kind. / 識別診斷種類的簡短機器可讀代碼。</param>
/// <param name="severity">The diagnostic severity. / 診斷嚴重程度。</param>
/// <param name="message">A human-readable description. / 人類可讀描述。</param>
/// <param name="packagePath">The package-relative path this diagnostic relates to, if any. / 此診斷相關的套件內相對路徑（若適用）。</param>
/// <param name="objectId">The identifier of the affected object (e.g. a shape or bookmark name), if any. / 受影響物件的識別碼（例如圖形或書籤名稱），若適用。</param>
/// <param name="location">A free-form location hint (e.g. a cell address or XPath), if any. / 自由格式的位置提示（例如儲存格位址或 XPath），若適用。</param>
public sealed class OdfDiagnostic(
    string code,
    OdfIssueSeverity severity,
    string message,
    string? packagePath = null,
    string? objectId = null,
    string? location = null)
{
    /// <summary>
    /// Gets the short machine-readable code identifying the diagnostic kind.
    /// 取得識別診斷種類的簡短機器可讀代碼。
    /// </summary>
    public string Code { get; } = code;

    /// <summary>
    /// Gets the diagnostic severity.
    /// 取得診斷嚴重程度。
    /// </summary>
    public OdfIssueSeverity Severity { get; } = severity;

    /// <summary>
    /// Gets the human-readable description.
    /// 取得人類可讀描述。
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// Gets the package-relative path this diagnostic relates to, if any.
    /// 取得此診斷相關的套件內相對路徑（若適用）。
    /// </summary>
    public string? PackagePath { get; } = packagePath;

    /// <summary>
    /// Gets the identifier of the affected object (e.g. a shape or bookmark name), if any.
    /// 取得受影響物件的識別碼（例如圖形或書籤名稱），若適用。
    /// </summary>
    public string? ObjectId { get; } = objectId;

    /// <summary>
    /// Gets a free-form location hint (e.g. a cell address or XPath), if any.
    /// 取得自由格式的位置提示（例如儲存格位址或 XPath），若適用。
    /// </summary>
    public string? Location { get; } = location;

    /// <summary>
    /// Maps a plain string collection (e.g. an existing warning or missing-name list) into typed
    /// diagnostics sharing one code and severity. Used internally so existing report types can
    /// expose a typed <c>Diagnostics</c> view alongside their original string collections without
    /// duplicating shape logic.
    /// 將既有的純字串集合（例如警告或缺漏名稱清單）對映為共用同一組 code 與 severity 的型別化
    /// 診斷。供內部使用，讓既有報告類別可在保留原始字串集合的同時，附加型別化的 Diagnostics 檢視，
    /// 不需重複撰寫對映邏輯。
    /// </summary>
    internal static IReadOnlyList<OdfDiagnostic> FromStrings(IEnumerable<string> values, string code, OdfIssueSeverity severity) =>
        values.Select(value => new OdfDiagnostic(code, severity, value)).ToList();
}
