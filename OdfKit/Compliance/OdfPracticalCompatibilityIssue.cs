using System.Collections.Generic;

namespace OdfKit.Compliance;

/// <summary>
/// Represents one practical interoperability risk.
/// 表示一項實務互通性風險。
/// </summary>
/// <param name="Severity">The issue severity. / 問題嚴重性。</param>
/// <param name="RuleId">The practical rule identifier. / 實務規則識別碼。</param>
/// <param name="MessageKey">The localized message key. / 本地化訊息鍵值。</param>
/// <param name="Message">The localized message. / 本地化訊息。</param>
/// <param name="Suggestion">The localized suggested action. / 本地化建議動作。</param>
/// <param name="DocumentKind">The document kind. / 文件種類。</param>
/// <param name="PackagePath">The related package path. / 相關封裝路徑。</param>
/// <param name="Details">The structured details. / 結構化細節。</param>
public sealed record OdfPracticalCompatibilityIssue(
    OdfIssueSeverity Severity,
    string RuleId,
    string MessageKey,
    string Message,
    string Suggestion,
    OdfDocumentKind DocumentKind,
    string? PackagePath = null,
    IReadOnlyDictionary<string, string?>? Details = null);
