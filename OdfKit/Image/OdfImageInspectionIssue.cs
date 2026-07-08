using OdfKit.Compliance;

namespace OdfKit.Image;

/// <summary>
/// Represents one practical image inspection issue.
/// 表示一個實務圖片檢查問題。
/// </summary>
/// <param name="RuleId">The stable rule identifier. / 穩定的規則識別碼。</param>
/// <param name="FrameName">The image frame name. / 影像框架名稱。</param>
/// <param name="ImageHref">The image resource path. / 影像資源路徑。</param>
/// <param name="Message">The issue message. / 問題訊息。</param>
/// <param name="Suggestion">The suggested fix. / 建議修正方式。</param>
/// <param name="Severity">The issue severity. / 問題嚴重性。</param>
/// <param name="Profile">The practical compatibility profile. / 實務相容性設定檔。</param>
/// <param name="MessageKey">The localized message key. / 本地化訊息鍵值。</param>
/// <param name="SuggestionKey">The localized suggestion key. / 本地化建議鍵值。</param>
public sealed record OdfImageInspectionIssue(
    string RuleId,
    string? FrameName,
    string? ImageHref,
    string Message,
    string Suggestion,
    OdfIssueSeverity Severity = OdfIssueSeverity.Warning,
    OdfPracticalCompatibilityProfile? Profile = null,
    string? MessageKey = null,
    string? SuggestionKey = null);
