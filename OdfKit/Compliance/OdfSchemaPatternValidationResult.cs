using System;
using System.Collections.Generic;

namespace OdfKit.Compliance;

/// <summary>
/// 代表結構描述模式驗證的結果。
/// </summary>
public sealed class OdfSchemaPatternValidationResult
{
    private OdfSchemaPatternValidationResult(bool isMatch, IReadOnlyList<OdfValidationIssue> issues)
    {
        IsMatch = isMatch;
        Issues = issues;
    }

    /// <summary>
    /// 取得一個值，表示 XML 元素是否符合結構描述模式。
    /// </summary>
    public bool IsMatch { get; }

    /// <summary>
    /// 取得模式驗證的問題。
    /// </summary>
    public IReadOnlyList<OdfValidationIssue> Issues { get; }

    internal static OdfSchemaPatternValidationResult Success()
    {
        return new OdfSchemaPatternValidationResult(true, []);
    }

    internal static OdfSchemaPatternValidationResult Fail(string ruleId, string message)
    {
        return new OdfSchemaPatternValidationResult(
            false,
            [
                new OdfValidationIssue(OdfIssueSeverity.Error, ruleId, message)
            ]);
    }
}
